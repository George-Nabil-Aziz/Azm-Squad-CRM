using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Portal;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.Auth;

public class DemoAccountsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record DemoBody(string Email, string Role, string? Password);

    private static WebApplicationFactory<Program> WithDemo(CrmApiFactory f, bool demo = true, bool logCodes = false) =>
        f.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:DemoAccounts"] = demo ? "true" : "false",
            ["Portal:LogLoginCodes"] = logCodes ? "true" : "false",
        })));

    [Fact]
    public async Task WithoutTheFlag_NoDemoAccountsAreSeeded_AndTheEndpointIs404()
    {
        using var plain = new CrmApiFactory();
        using var scope = plain.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("agent@crm.com"));

        var response = await plain.CreateClient().GetAsync("/api/auth/demo-accounts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithTheFlag_EachRoleUserExists_ActiveWithTheSeedPassword_AndSeedingTwiceDoesNotDuplicate()
    {
        using var app = WithDemo(factory);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var (email, role) in new[]
                 {
                     ("superadmin@crm.com", "SuperAdmin"), ("admin@crm.com", "Admin"),
                     ("supervisor@crm.com", "Supervisor"), ("agent@crm.com", "Agent"),
                 })
        {
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.True(user.IsActive);
            Assert.Equal([role], await users.GetRolesAsync(user));
            Assert.True(await users.CheckPasswordAsync(user, CrmApiFactory.SuperAdminPassword));
        }

        await scope.ServiceProvider.GetRequiredService<CrmDbInitializer>().InitializeAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(4, await db.Users.CountAsync(u => u.Email!.EndsWith("@crm.com")));
        Assert.Equal(1, await db.Customers.CountAsync(c => c.Contacts.Any(x => x.Value == "customer@crm.com")));
    }

    [Fact]
    public async Task TheEndpoint_ListsTheDemoAccounts_StaffWithTheSeedPassword_CustomerWithout()
    {
        using var app = WithDemo(factory);

        var response = await app.CreateClient().GetAsync("/api/auth/demo-accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<DemoBody>>();
        Assert.Equal(
            [("superadmin@crm.com", "SuperAdmin"), ("admin@crm.com", "Admin"), ("supervisor@crm.com", "Supervisor"),
             ("agent@crm.com", "Agent"), ("customer@crm.com", "Customer")],
            list!.Select(a => (a.Email, a.Role)));
        Assert.All(list.Where(a => a.Role != "Customer"), a => Assert.Equal(CrmApiFactory.SuperAdminPassword, a.Password));
        Assert.Null(list.Single(a => a.Role == "Customer").Password);
    }

    [Fact]
    public async Task ADemoStaffAccount_CanSignIn()
    {
        using var app = WithDemo(factory);

        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "agent@crm.com", password = CrmApiFactory.SuperAdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheDemoCustomer_CanSignInToThePortalWithTheCode()
    {
        using var app = WithDemo(factory);
        var portal = PortalApp.Create(factory);
        _ = app;
        await portal.RequestCodeAsync("customer@crm.com");
        var code = portal.Email.LastCodeFor("customer@crm.com");

        var response = await portal.VerifyAsync("customer@crm.com", code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ALegacyAdminSuperAdmin_IsRenamedToSuperAdmin_AndTheDemoAdminTakesItsEmail()
    {
        using var legacy = new CrmApiFactory();
        using (var scope = legacy.Services.CreateScope())
        {
            // Simulate an old development database: the SuperAdmin is admin@crm.local.
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = (await users.FindByEmailAsync("superadmin@crm.com"))!;
            await users.SetEmailAsync(admin, "admin@crm.local");
            await users.SetUserNameAsync(admin, "admin@crm.local");
        }

        using var app = WithDemo(legacy);
        using var after = app.Services.CreateScope();
        var manager = after.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var renamed = await manager.FindByEmailAsync("superadmin@crm.com");
        Assert.NotNull(renamed);
        Assert.Equal("superadmin@crm.com", renamed.UserName);
        Assert.True(await manager.IsInRoleAsync(renamed, "SuperAdmin"));
        Assert.Null(await manager.FindByEmailAsync("admin@crm.local"));
        var demoAdmin = await manager.FindByEmailAsync("admin@crm.com");
        Assert.NotNull(demoAdmin);
        Assert.True(await manager.IsInRoleAsync(demoAdmin, "Admin"));
    }

    [Fact]
    public async Task ThePortalCode_IsLoggedOnlyWhenDevelopmentLoggingIsOn()
    {
        var email = PortalApp.NewEmail();
        var off = PortalApp.Create(factory);
        await off.RequestCodeAsync(email);
        var code = off.Email.LastCodeFor(email);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Message.Contains(code) && e.Message.Contains("Portal login code"));

        var onEmail = PortalApp.NewEmail();
        var on = PortalApp.Create(factory, new Dictionary<string, string?> { ["Portal:LogLoginCodes"] = "true" });
        await on.RequestCodeAsync(onEmail);
        var onCode = on.Email.LastCodeFor(onEmail);
        Assert.Contains(factory.Logs.Entries,
            e => e.Level == LogLevel.Information && e.Message == $"Portal login code for {onEmail}: {onCode}");
    }
}
