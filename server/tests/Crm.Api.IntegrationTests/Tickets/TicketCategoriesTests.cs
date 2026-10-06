using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Tickets;

public class TicketCategoriesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>A name no other test of the class uses (the database is shared by the tests of a class).</summary>
    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 13)];

    private static async Task<TicketCategoryBody> CreateAsync(HttpClient admin, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/ticket-categories", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketCategoryBody>())!;
    }

    [Fact]
    public async Task AdminCreatesACategory_ItIsListedAsActive_ForAnAgent()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var name = UniqueName("Billing");

        var response = await admin.PostAsJsonAsync("/api/ticket-categories", new { name });
        var created = await response.Content.ReadFromJsonAsync<TicketCategoryBody>();
        var active = await agent.GetFromJsonAsync<TicketCategoryBody[]>("/api/ticket-categories?activeOnly=true");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/ticket-categories/{created!.Id}", response.Headers.Location?.OriginalString);
        Assert.True(created.IsActive);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Contains(active!, c => c.Id == created.Id && c.Name == name && c.IsActive);
    }

    [Fact]
    public async Task List_IsOrderedByName()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var tag = UniqueName("Order");
        await CreateAsync(admin, $"{tag} b");
        await CreateAsync(admin, $"{tag} a");

        var all = await admin.GetFromJsonAsync<TicketCategoryBody[]>("/api/ticket-categories");

        Assert.Equal([$"{tag} a", $"{tag} b"], all!.Where(c => c.Name.StartsWith(tag, StringComparison.Ordinal)).Select(c => c.Name));
    }

    [Fact]
    public async Task CreatingADuplicateName_IgnoringCaseAndSpaces_Returns400_WithTheNameField()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var name = UniqueName("Duplicate");
        await CreateAsync(admin, name);

        var response = await admin.PostAsJsonAsync("/api/ticket-categories", new { name = $"  {name.ToUpperInvariant()} " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["name"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task RenamingToAnExistingName_Returns400()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var first = await CreateAsync(admin, UniqueName("First"));
        var second = await CreateAsync(admin, UniqueName("Second"));

        var response = await admin.PutAsJsonAsync($"/api/ticket-categories/{second.Id}", new { name = first.Name, isActive = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("name", problem!.Errors.Keys);
    }

    [Fact]
    public async Task DeactivatedCategory_IsNotInTheActiveList_ButStaysInTheFullList()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var created = await CreateAsync(admin, UniqueName("Legacy"));

        var response = await admin.PutAsJsonAsync($"/api/ticket-categories/{created.Id}", new { name = created.Name, isActive = false });
        var active = await admin.GetFromJsonAsync<TicketCategoryBody[]>("/api/ticket-categories?activeOnly=true");
        var all = await admin.GetFromJsonAsync<TicketCategoryBody[]>("/api/ticket-categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(active!, c => c.Id == created.Id);
        Assert.Contains(all!, c => c.Id == created.Id && !c.IsActive);
    }

    [Fact]
    public async Task Update_UnknownCategory_Returns404()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var response = await admin.PutAsJsonAsync($"/api/ticket-categories/{Guid.NewGuid()}", new { name = "Anything" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAName_Returns400()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var response = await admin.PostAsJsonAsync("/api/ticket-categories", new { name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["name"], problem!.Errors.Keys);
    }
}
