using Crm.Application.Auth;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Runs once at startup: prepares the database according to <c>Database:StartupAction</c>
/// (<c>Migrate</c> | <c>EnsureCreated</c> | anything else = nothing) and seeds the four roles and the
/// SuperAdmin user. Idempotent: safe to run on every start.
/// </summary>
public sealed class CrmDbInitializer(
    CrmDbContext db,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<CrmDbInitializer> logger)
{
    public const string SuperAdminEmail = "admin@crm.local";
    public const string SuperAdminFullName = "System Administrator";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        switch (configuration["Database:StartupAction"])
        {
            case "Migrate":
                await db.Database.MigrateAsync(cancellationToken);
                break;
            case "EnsureCreated":
                await db.Database.EnsureCreatedAsync(cancellationToken);
                break;
        }

        await SeedRolesAsync();
        await SeedSuperAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                ThrowIfFailed(await roleManager.CreateAsync(new ApplicationRole(role)), $"create role '{role}'");
            }
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        if (await userManager.FindByEmailAsync(SuperAdminEmail) is not null)
        {
            return;
        }

        var password = configuration["Seed:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed:SuperAdminPassword is not configured; SuperAdmin user {Email} was not created.", SuperAdminEmail);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = SuperAdminEmail,
            Email = SuperAdminEmail,
            EmailConfirmed = true,
            FullName = SuperAdminFullName,
        };
        ThrowIfFailed(await userManager.CreateAsync(admin, password), "create the SuperAdmin user");
        ThrowIfFailed(await userManager.AddToRoleAsync(admin, Roles.SuperAdmin), "add the SuperAdmin role");
        logger.LogInformation("Seeded SuperAdmin user {Email}.", SuperAdminEmail);
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Seeding failed to {action}: {errors}");
        }
    }
}
