using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Users;

public class UserManagementTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string UsersPath = "/api/users";
    private const string Password = "Agent#Pass123";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@crm.local";

    private async Task<HttpClient> AdminClientAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    private static async Task<UserBody> CreateAsync(HttpClient admin, string email, params string[] roles)
    {
        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email, fullName = "New Agent", password = Password, roles });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserBody>())!;
    }

    private Task<HttpResponseMessage> LoginAsync(string email) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });

    [Fact]
    public async Task CreateUser_ThenTheNewUserCanLogIn()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email, fullName = "  Sara Agent  ", password = Password, roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserBody>();
        Assert.Equal($"/api/users/{user!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(email, user.Email);
        Assert.Equal("Sara Agent", user.FullName);
        Assert.Equal(["Agent"], user.Roles);
        Assert.True(user.IsActive);

        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData(CrmApiFactory.SuperAdminEmail)]
    [InlineData("ADMIN@CRM.LOCAL")]
    public async Task CreateUser_WithExistingEmail_Returns400WithEmailError(string email)
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email, fullName = "Copy", password = Password, roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["This email is already used by another user."], problem!.Errors["email"]);
    }

    [Fact]
    public async Task CreateUser_WithInvalidData_Returns400WithFieldErrors()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email = "not-an-email", fullName = "", password = "weak", roles = new[] { "Pilot" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["email", "fullName", "password", "roles"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetUser_ReturnsTheUser_AndUnknownIdReturns404()
    {
        var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewEmail(), "Supervisor");

        var found = await admin.GetFromJsonAsync<UserBody>($"{UsersPath}/{created.Id}");
        var missing = await admin.GetAsync($"{UsersPath}/{Guid.NewGuid()}");

        Assert.Equal(created.Email, found!.Email);
        Assert.Equal(["Supervisor"], found.Roles);
        Assert.True(found.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var problem = await missing.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The user was not found.", problem!.Detail);
    }

    [Fact]
    public async Task UpdateUser_ChangesNameEmailAndRoles()
    {
        var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewEmail(), "Agent");
        var newEmail = NewEmail();

        var response = await admin.PutAsJsonAsync($"{UsersPath}/{created.Id}",
            new { email = newEmail, fullName = "Renamed", roles = new[] { "Supervisor", "Agent" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await admin.GetFromJsonAsync<UserBody>($"{UsersPath}/{created.Id}");
        Assert.Equal(newEmail, updated!.Email);
        Assert.Equal("Renamed", updated.FullName);
        Assert.Equal(["Agent", "Supervisor"], updated.Roles);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(newEmail)).StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithEmailOfAnotherUser_Returns400()
    {
        var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewEmail(), "Agent");

        var response = await admin.PutAsJsonAsync($"{UsersPath}/{created.Id}",
            new { email = CrmApiFactory.SuperAdminEmail, fullName = "Thief", roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("email", problem!.Errors.Keys);
    }

    [Fact]
    public async Task UpdateUser_UnknownId_Returns404()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"{UsersPath}/{Guid.NewGuid()}",
            new { email = NewEmail(), fullName = "Nobody", roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_TryingToLogIn_Gets401()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var created = await CreateAsync(admin, email, "Agent");

        var deactivate = await admin.PostAsync($"{UsersPath}/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var problem = await login.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Detail);
        var user = await admin.GetFromJsonAsync<UserBody>($"{UsersPath}/{created.Id}");
        Assert.False(user!.IsActive);
    }

    [Fact]
    public async Task DeactivatedUser_ExistingToken_StopsWorking()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var created = await CreateAsync(admin, email, "Agent");
        var agent = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, Password));
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/api/auth/me")).StatusCode);

        await admin.PostAsync($"{UsersPath}/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ReactivatedUser_CanLogInAgain()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var created = await CreateAsync(admin, email, "Agent");
        await admin.PostAsync($"{UsersPath}/{created.Id}/deactivate", null);

        var reactivate = await admin.PostAsync($"{UsersPath}/{created.Id}/reactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Deactivate_YourOwnAccount_Returns409()
    {
        var admin = await AdminClientAsync();
        var me = await admin.GetFromJsonAsync<UserBody>("/api/auth/me");

        var response = await admin.PostAsync($"{UsersPath}/{me!.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("You cannot deactivate your own account.", problem!.Detail);
    }

    [Fact]
    public async Task Admin_CannotCreateASuperAdmin_Returns403()
    {
        var admin = await factory.CreateClientWithRoleAsync("Admin");

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email = NewEmail(), fullName = "Escalation", password = Password, roles = new[] { "SuperAdmin" } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeactivateTheSuperAdmin_Returns403()
    {
        var superAdmin = await AdminClientAsync();
        var superAdminId = (await superAdmin.GetFromJsonAsync<UserBody>("/api/auth/me"))!.Id;
        var admin = await factory.CreateClientWithRoleAsync("Admin");

        var response = await admin.PostAsync($"{UsersPath}/{superAdminId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await superAdmin.GetAsync("/api/auth/me")).StatusCode);
    }
}
