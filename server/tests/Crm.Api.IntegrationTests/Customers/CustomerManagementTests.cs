using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomerManagementTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string CustomersPath = "/api/customers";

    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    private static async Task<CustomerBody> CreateAsync(HttpClient client, string name, string? email = null, string? phone = null)
    {
        var response = await client.PostAsJsonAsync(CustomersPath, new { name, email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    [Fact]
    public async Task CreateCustomer_WithAName_Returns201WithLocationAndTheProfile()
    {
        var agent = await AgentClientAsync();

        var response = await agent.PostAsJsonAsync(CustomersPath,
            new { name = "  Nour Trading  ", email = "info@nour.example", phone = "+966 50 123 4567" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerBody>();
        Assert.Equal($"/api/customers/{customer!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Nour Trading", customer.Name);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal("+966 50 123 4567", customer.Phone);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, customer.CreatedAt);
        Assert.Equal(customer.CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public async Task CreateCustomer_WithOnlyAName_Returns201WithoutEmailAndPhone()
    {
        var agent = await AgentClientAsync();

        var customer = await CreateAsync(agent, "Walk-in customer", email: "", phone: "  ");

        Assert.Null(customer.Email);
        Assert.Null(customer.Phone);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":""}""")]
    [InlineData("""{"name":"   ","email":"info@nour.example"}""")]
    public async Task CreateCustomer_WithoutAName_Returns400WithNameError(string body)
    {
        var agent = await AgentClientAsync();

        var response = await agent.PostAsync(CustomersPath, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["name"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmailAndPhone_Returns400WithFieldErrors()
    {
        var agent = await AgentClientAsync();

        var response = await agent.PostAsJsonAsync(CustomersPath, new { name = "Nour", email = "not-an-email", phone = "call me" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["email", "phone"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetCustomer_ReturnsTheProfile_WithUtcTimes()
    {
        var agent = await AgentClientAsync();
        var created = await CreateAsync(agent, "Nour", "info@nour.example");

        var response = await agent.GetAsync($"{CustomersPath}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Nour", json.RootElement.GetProperty("name").GetString());
        // Read back from the database: still marked as UTC ("Z"), so browsers show the right local time.
        Assert.EndsWith("Z", json.RootElement.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task GetCustomer_UnknownId_Returns404()
    {
        var agent = await AgentClientAsync();

        var response = await agent.GetAsync($"{CustomersPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The customer was not found.", problem!.Detail);
    }

    [Fact]
    public async Task UpdateCustomer_ChangesTheProfile_AndUpdatedAt()
    {
        var agent = await AgentClientAsync();
        var created = await CreateAsync(agent, "Nour", "old@nour.example", "0501234567");
        factory.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await agent.PutAsJsonAsync($"{CustomersPath}/{created.Id}",
            new { name = "Nour Trading Co.", email = "", phone = "+966501234567" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await agent.GetFromJsonAsync<CustomerBody>($"{CustomersPath}/{created.Id}");
        Assert.Equal("Nour Trading Co.", updated!.Name);
        Assert.Null(updated.Email);
        Assert.Equal("+966501234567", updated.Phone);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCustomer_WithoutAName_Returns400_AndKeepsTheProfile()
    {
        var agent = await AgentClientAsync();
        var created = await CreateAsync(agent, "Nour");

        var response = await agent.PutAsJsonAsync($"{CustomersPath}/{created.Id}", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var current = await agent.GetFromJsonAsync<CustomerBody>($"{CustomersPath}/{created.Id}");
        Assert.Equal("Nour", current!.Name);
    }

    [Fact]
    public async Task UpdateCustomer_UnknownId_Returns404()
    {
        var agent = await AgentClientAsync();

        var response = await agent.PutAsJsonAsync($"{CustomersPath}/{Guid.NewGuid()}", new { name = "Nobody" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCustomer_HidesIt_ButKeepsTheRow()
    {
        var agent = await AgentClientAsync();
        var tag = $"del{Guid.NewGuid():N}"[..12];
        var created = await CreateAsync(agent, $"{tag} Customer");
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        var deletedAt = factory.Time.GetUtcNow().UtcDateTime;

        var response = await agent.DeleteAsync($"{CustomersPath}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Gone from the list, from GET, and cannot be edited or deleted again.
        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"{CustomersPath}?search={tag}");
        Assert.Empty(page!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"{CustomersPath}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await agent.PutAsJsonAsync($"{CustomersPath}/{created.Id}", new { name = "Back" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.DeleteAsync($"{CustomersPath}/{created.Id}")).StatusCode);

        // Soft delete: the row and its data stay in the database, so the customer's tickets keep their customer.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var row = await db.Set<Customer>().IgnoreQueryFilters().SingleAsync(c => c.Id == created.Id);
        Assert.True(row.IsDeleted);
        Assert.Equal(deletedAt, row.DeletedAt);
        Assert.Equal(DateTimeKind.Utc, row.DeletedAt!.Value.Kind); // nullable DateTime is read back as UTC too
        Assert.Equal($"{tag} Customer", row.Name);
    }

    [Fact]
    public async Task DeleteCustomer_UnknownId_Returns404()
    {
        var agent = await AgentClientAsync();

        var response = await agent.DeleteAsync($"{CustomersPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
