using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomerListTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    /// <summary>
    /// Creates customers "&lt;tag&gt; n" with email "&lt;tag&gt;-n@example.test" and phone "+9665&lt;digits&gt;n"
    /// (unique per test: the database is shared by the tests of the class). Returns the tag and the phone prefix.
    /// </summary>
    private async Task<(string Tag, string PhonePrefix)> CreateCustomersAsync(HttpClient client, int count)
    {
        var tag = $"c{Guid.NewGuid():N}"[..12];
        var phonePrefix = $"+9665{Random.Shared.Next(10_000_000, 99_999_999)}";
        for (var n = 1; n <= count; n++)
        {
            var response = await client.PostAsJsonAsync("/api/customers",
                new { name = $"{tag} {n}", email = $"{tag}-{n}@example.test", phone = $"{phonePrefix}{n}" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        return (tag, phonePrefix);
    }

    [Fact]
    public async Task List_WithoutParameters_ReturnsFirstPageOf20WithTotalCount()
    {
        var agent = await AgentClientAsync();
        await CreateCustomersAsync(agent, 1);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>("/api/customers");

        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 1);
        Assert.NotEmpty(page.Items);
    }

    [Fact]
    public async Task List_SearchByName_ReturnsOnlyMatchingCustomers()
    {
        var agent = await AgentClientAsync();
        var (tag, _) = await CreateCustomersAsync(agent, 3);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag} 2");

        Assert.Equal($"{tag} 2", Assert.Single(page!.Items).Name);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_SearchByPhone_ReturnsTheCustomerWithThatNumber()
    {
        var agent = await AgentClientAsync();
        var (tag, phonePrefix) = await CreateCustomersAsync(agent, 2);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>(
            $"/api/customers?search={Uri.EscapeDataString(phonePrefix + "2")}");

        var customer = Assert.Single(page!.Items);
        Assert.Equal($"{tag} 2", customer.Name);
        Assert.Equal($"{phonePrefix}2", customer.Phone);
    }

    [Fact]
    public async Task List_SearchByEmail_IsCaseInsensitive()
    {
        var agent = await AgentClientAsync();
        var (tag, _) = await CreateCustomersAsync(agent, 2);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag.ToUpperInvariant()}-1@EXAMPLE");

        Assert.Equal($"{tag}-1@example.test", Assert.Single(page!.Items).Email);
    }

    [Fact]
    public async Task List_SearchTreatsWildcardsAsText()
    {
        var agent = await AgentClientAsync();
        await CreateCustomersAsync(agent, 1);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>("/api/customers?search=%25");

        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task List_Paginates_OrderedByName_WithTotalCountOfAllMatches()
    {
        var agent = await AgentClientAsync();
        var (tag, _) = await CreateCustomersAsync(agent, 3);

        var first = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag}&page=1&pageSize=2");
        var second = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag}&page=2&pageSize=2");

        Assert.Equal([$"{tag} 1", $"{tag} 2"], first!.Items.Select(c => c.Name));
        Assert.Equal([$"{tag} 3"], second!.Items.Select(c => c.Name));
        Assert.All([first, second], page => Assert.Equal(3, page.TotalCount));
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task List_WithInvalidPaging_Returns400(string queryString, string field)
    {
        var agent = await AgentClientAsync();

        var response = await agent.GetAsync($"/api/customers?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }
}
