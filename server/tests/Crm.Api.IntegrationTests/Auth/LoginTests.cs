using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Crm.Api.IntegrationTests.Auth;

public class LoginTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string LoginPath = "/api/auth/login";
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithAccessToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath,
            new { email = CrmApiFactory.SuperAdminEmail, password = CrmApiFactory.SuperAdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CrmApiFactory.LoginBody>();
        Assert.NotNull(body);
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(factory.Time.GetUtcNow().AddMinutes(60), body.ExpiresAt, TimeSpan.FromSeconds(1));

        var token = new JsonWebToken(body.AccessToken);
        Assert.Equal("Crm.Api", token.Issuer);
        Assert.Equal(["Crm.Client"], token.Audiences);
        Assert.Equal(CrmApiFactory.SuperAdminEmail, token.GetClaim("email").Value);
        Assert.True(Guid.TryParse(token.Subject, out _));
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "SuperAdmin");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithoutToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath,
            new { email = CrmApiFactory.SuperAdminEmail, password = "Wrong#Password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
        Assert.Equal("Invalid email or password.", problem.Detail);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401WithSameMessage()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath,
            new { email = "nobody@crm.local", password = CrmApiFactory.SuperAdminPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Detail);
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400WithFieldErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath, new { email = "not-an-email", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("email", problem!.Errors.Keys);
        Assert.Contains("password", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_IsLockedOutEvenWithCorrectPassword()
    {
        const string email = "lockout@crm.local";
        const string password = "Lockout#123";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await users.CreateAsync(
                new ApplicationUser { UserName = email, Email = email, FullName = "Lockout User" }, password);
            Assert.True(result.Succeeded);
        }

        var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync(LoginPath, new { email, password = "Wrong#Password1" });
        }

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
