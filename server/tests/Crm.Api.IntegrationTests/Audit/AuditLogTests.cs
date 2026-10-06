using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Users;

namespace Crm.Api.IntegrationTests.Audit;

public sealed record AuditLogBody(
    long Id, DateTime OccurredAt, Guid? UserId, string? UserEmail, string Action, string EntityType, string? EntityId,
    string? OldValues, string? NewValues, string? IpAddress);

public sealed record AuditLogPageBody(AuditLogBody[] Items, int Page, int PageSize, int TotalCount);

public class AuditLogTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<HttpClient> SuperAdminAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    private static async Task<AuditLogPageBody> ListAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/audit-logs{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuditLogPageBody>())!;
    }

    private static async Task<UserBody> CreateUserAsync(HttpClient client, string email, params string[] roles)
    {
        var response = await client.PostAsJsonAsync("/api/users",
            new { email, fullName = "Audited User", password = "Audit#12345", roles });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserBody>())!;
    }

    [Fact]
    public async Task CreatingAUser_IsLogged_WithUserActionEntityValuesIpAndTime()
    {
        var superAdmin = await SuperAdminAsync();
        var email = $"audited-{Guid.NewGuid():N}@crm.local";
        var before = DateTime.UtcNow.AddMinutes(-1);

        var user = await CreateUserAsync(superAdmin, email, "Agent");
        var page = await ListAsync(superAdmin, $"?action=user.created&pageSize=100");

        var entry = page.Items.Single(e => e.EntityId == user.Id.ToString());
        Assert.Equal(CrmApiFactory.SuperAdminEmail, entry.UserEmail);
        Assert.NotNull(entry.UserId);
        Assert.Equal("User", entry.EntityType);
        Assert.Null(entry.OldValues);
        Assert.Contains(email, entry.NewValues);
        Assert.Contains("Agent", entry.NewValues);
        Assert.DoesNotContain("Audit#12345", entry.NewValues);
        Assert.True(entry.OccurredAt > before);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAt.Kind);
        Assert.False(string.IsNullOrWhiteSpace(entry.IpAddress));
    }

    [Fact]
    public async Task ChangingARole_IsLogged_WithOldAndNewRoles()
    {
        var superAdmin = await SuperAdminAsync();
        var user = await CreateUserAsync(superAdmin, $"role-{Guid.NewGuid():N}@crm.local", "Agent");

        var update = await superAdmin.PutAsJsonAsync($"/api/users/{user.Id}",
            new { email = user.Email, fullName = user.FullName, roles = new[] { "Supervisor" } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var page = await ListAsync(superAdmin, "?action=user.updated&pageSize=100");
        var entry = page.Items.Single(e => e.EntityId == user.Id.ToString());
        Assert.Contains("Agent", entry.OldValues);
        Assert.Contains("Supervisor", entry.NewValues);
    }

    [Fact]
    public async Task DeactivatingAUser_IsLogged()
    {
        var superAdmin = await SuperAdminAsync();
        var user = await CreateUserAsync(superAdmin, $"off-{Guid.NewGuid():N}@crm.local", "Agent");

        await superAdmin.PostAsync($"/api/users/{user.Id}/deactivate", null);

        var page = await ListAsync(superAdmin, "?action=user.deactivated&pageSize=100");
        Assert.Contains(page.Items, e => e.EntityId == user.Id.ToString());
    }

    [Fact]
    public async Task ASlaChange_IsLogged_WithOldAndNewTimes()
    {
        var superAdmin = await SuperAdminAsync();

        var response = await superAdmin.PutAsJsonAsync("/api/sla-policies/low", new { responseMinutes = 31, resolutionMinutes = 4321 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entry = (await ListAsync(superAdmin, "?action=sla-policy.updated&pageSize=100")).Items.First(e => e.EntityId == "low");
        Assert.Contains("\"responseMinutes\":31", entry.NewValues);
        Assert.Contains("\"responseMinutes\":", entry.OldValues);
    }

    [Fact]
    public async Task DeletingACustomer_IsLogged()
    {
        var superAdmin = await SuperAdminAsync();
        var created = await superAdmin.PostAsJsonAsync("/api/customers", new { name = $"Doomed {Guid.NewGuid():N}" });
        var id = (await created.Content.ReadFromJsonAsync<Customers.CustomerBody>())!.Id;

        var deleted = await superAdmin.DeleteAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var entry = (await ListAsync(superAdmin, "?action=customer.deleted&pageSize=100")).Items.Single(e => e.EntityId == id.ToString());
        Assert.Contains("Doomed", entry.OldValues);
    }

    [Fact]
    public async Task ALogin_IsLogged_AndAFailedLogin_IsLoggedWithoutThePassword()
    {
        var superAdmin = await SuperAdminAsync();
        var email = $"login-{Guid.NewGuid():N}@crm.local";
        var userId = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, "Agent");

        var wrong = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong#Password1" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        await factory.LoginAsync(email, CrmApiFactory.TestUserPassword);
        var unknown = $"nobody-{Guid.NewGuid():N}@crm.local";
        await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = unknown, password = "Wrong#Password1" });

        var failed = (await ListAsync(superAdmin, "?action=login.failed&pageSize=100")).Items;
        var known = failed.Single(e => e.UserId == userId);
        Assert.Equal(email, known.UserEmail);
        Assert.DoesNotContain("Wrong#Password1", known.NewValues ?? string.Empty);
        Assert.Contains(failed, e => e.UserId is null && e.UserEmail == unknown);
        var succeeded = (await ListAsync(superAdmin, $"?action=login.succeeded&userId={userId}")).Items;
        Assert.Single(succeeded);
    }

    [Fact]
    public async Task Logs_CanBeFilteredByUserActionAndDate()
    {
        var superAdmin = await SuperAdminAsync();
        var user = await CreateUserAsync(superAdmin, $"filter-{Guid.NewGuid():N}@crm.local", "Agent");
        await superAdmin.PostAsync($"/api/users/{user.Id}/deactivate", null);

        var byUser = await ListAsync(superAdmin, $"?userId={await UserIdOfAsync(superAdmin)}&action=user.deactivated&pageSize=100");
        Assert.All(byUser.Items, e => Assert.Equal("user.deactivated", e.Action));
        Assert.Contains(byUser.Items, e => e.EntityId == user.Id.ToString());

        var future = Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"));
        var none = await ListAsync(superAdmin, $"?from={future}");
        Assert.Empty(none.Items);
        Assert.Equal(0, none.TotalCount);

        var past = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"));
        var recent = await ListAsync(superAdmin, $"?from={past}&to={future}&pageSize=1");
        Assert.Single(recent.Items);
        Assert.True(recent.TotalCount > 1);
        Assert.Equal(1, recent.PageSize);
    }

    [Fact]
    public async Task Logs_AreNewestFirst()
    {
        var items = (await ListAsync(await SuperAdminAsync(), "?pageSize=100")).Items;

        Assert.Equal(items.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.Id), items.Select(e => e.Id));
    }

    [Theory]
    [InlineData("?from=2026-10-02T00:00:00Z&to=2026-10-01T00:00:00Z", "from")]
    [InlineData("?action=made.up", "action")]
    [InlineData("?pageSize=101", "pageSize")]
    public async Task InvalidFilters_Return400_WithTheField(string query, string field)
    {
        var response = await (await SuperAdminAsync()).GetAsync($"/api/audit-logs{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Supervisor", HttpStatusCode.Forbidden)]
    [InlineData("Agent", HttpStatusCode.Forbidden)]
    public async Task OnlySuperAdminAndAdmin_CanView(string role, HttpStatusCode expected)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        Assert.Equal(expected, (await client.GetAsync("/api/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task WithoutAToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/audit-logs")).StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task TheLog_IsReadOnly(string method)
    {
        var superAdmin = await SuperAdminAsync();

        var response = await superAdmin.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api/audit-logs")
        {
            Content = JsonContent.Create(new { action = "user.created" }),
        });

        Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, response.StatusCode.ToString());
    }

    private async Task<Guid> UserIdOfAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<UserBody>("/api/auth/me");
        return me!.Id;
    }
}
