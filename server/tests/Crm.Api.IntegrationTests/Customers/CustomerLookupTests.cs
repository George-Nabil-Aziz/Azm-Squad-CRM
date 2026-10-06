using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-9 AC 4: lookup by phone or email (used by the email / WhatsApp channels), and search by any contact.</summary>
public class CustomerLookupTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    private static async Task<CustomerBody> CreateCustomerAsync(HttpClient client, string name, string? phone = null, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    private static async Task<CustomerBody[]> LookupAsync(HttpClient client, string queryString)
    {
        var response = await client.GetAsync($"/api/customers/lookup?{queryString}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody[]>())!;
    }

    private static string Unique() => $"l{Guid.NewGuid():N}"[..12];

    [Fact]
    public async Task Lookup_ByPhone_ReturnsTheMatchingCustomer()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);
        await CreateCustomerAsync(agent, "Someone else", TestPhones.NewMobile());

        var found = await LookupAsync(agent, $"phone={Uri.EscapeDataString(phone)}");

        var match = Assert.Single(found);
        Assert.Equal(customer.Id, match.Id);
        Assert.Equal("Nour Trading", match.Name);
        Assert.Equal(phone, Assert.Single(match.Contacts).Value);
    }

    [Fact]
    public async Task Lookup_ByPhoneTypedLocally_FindsTheE164Number()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);

        var found = await LookupAsync(agent, $"phone={TestPhones.Local(phone)}");

        Assert.Equal(customer.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Lookup_ByPhone_FindsAWhatsAppNumber_AsWhatsAppSendsIt()
    {
        var agent = await AgentClientAsync();
        var whatsApp = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading");
        var added = await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", new { type = "whatsapp", value = whatsApp });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        // WhatsApp Cloud API sends the sender as digits without "+" ("966501234567").
        var found = await LookupAsync(agent, $"phone={whatsApp.TrimStart('+')}");

        Assert.Equal(customer.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Lookup_ByASecondaryEmail_IsCaseInsensitive()
    {
        var agent = await AgentClientAsync();
        var tag = Unique();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", email: $"info-{tag}@nour.example");
        var added = await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts",
            new { type = "email", value = $"sales-{tag}@nour.example" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var found = await LookupAsync(agent, $"email={Uri.EscapeDataString($"SALES-{tag.ToUpperInvariant()}@Nour.Example")}");

        Assert.Equal(customer.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Lookup_ReturnsEveryCustomerSharingTheNumber_OrderedByName()
    {
        var agent = await AgentClientAsync();
        var switchboard = TestPhones.NewMobile();
        var tag = Unique();
        await CreateCustomerAsync(agent, $"{tag} B", switchboard);
        await CreateCustomerAsync(agent, $"{tag} A", switchboard);

        var found = await LookupAsync(agent, $"phone={Uri.EscapeDataString(switchboard)}");

        Assert.Equal([$"{tag} A", $"{tag} B"], found.Select(c => c.Name));
    }

    [Fact]
    public async Task Lookup_DoesNotReturnDeletedCustomers()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);
        await agent.DeleteAsync($"/api/customers/{customer.Id}");

        var found = await LookupAsync(agent, $"phone={Uri.EscapeDataString(phone)}");

        Assert.Empty(found);
    }

    [Fact]
    public async Task Lookup_WithoutMatch_ReturnsAnEmptyList()
    {
        var agent = await AgentClientAsync();

        var found = await LookupAsync(agent, $"email=nobody-{Unique()}@nour.example");

        Assert.Empty(found);
    }

    [Theory]
    [InlineData("", "phone")]
    [InlineData("phone=12345", "phone")]
    [InlineData("email=not-an-email", "email")]
    [InlineData("phone=0501234567&email=info@nour.example", "email")]
    public async Task Lookup_WithInvalidOrMissingPhoneOrEmail_Returns400(string queryString, string field)
    {
        var agent = await AgentClientAsync();

        var response = await agent.GetAsync($"/api/customers/lookup?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
    }

    [Fact]
    public async Task List_SearchByASecondaryContact_FindsTheCustomer()
    {
        var agent = await AgentClientAsync();
        var tag = Unique();
        var customer = await CreateCustomerAsync(agent, "Nour Trading");
        await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", new { type = "email", value = $"{tag}@nour.example" });

        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag}");

        Assert.Equal(customer.Id, Assert.Single(page!.Items).Id);
    }

    [Fact]
    public async Task List_SearchByPhoneTypedLocally_FindsTheE164Number()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>(
            $"/api/customers?search={Uri.EscapeDataString(TestPhones.Local(phone))}");

        Assert.Equal(customer.Id, Assert.Single(page!.Items).Id);
    }
}
