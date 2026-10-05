using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Auth;

public class SeedTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task Startup_SeedsRolesAndSuperAdmin()
    {
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roleNames = await roles.Roles.Select(r => r.Name!).ToListAsync();
        Assert.Equal(["Admin", "Agent", "SuperAdmin", "Supervisor"], roleNames.Order(StringComparer.Ordinal));
        var admin = await users.FindByEmailAsync(CrmApiFactory.SuperAdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin, "SuperAdmin"));
    }

    [Fact]
    public async Task Initializer_RunTwice_DoesNotDuplicateData()
    {
        using var scope = factory.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<CrmDbInitializer>();

        await initializer.InitializeAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == CrmApiFactory.SuperAdminEmail));
    }
}
