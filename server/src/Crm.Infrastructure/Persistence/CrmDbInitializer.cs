using Crm.Application.Auth;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Crm.Domain.Customers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<CrmDbInitializer> logger)
{
    public const string SuperAdminEmail = DemoAccounts.SuperAdminEmail;
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
        var demo = DemoAccounts.IsEnabled(environment.EnvironmentName, configuration["Seed:DemoAccounts"]);
        if (demo)
        {
            await RenameLegacyAdminAsync();
        }

        await SeedSuperAdminAsync();
        if (demo)
        {
            await SeedDemoAccountsAsync();
        }
    }

    /// <summary>
    /// Development databases created before the role-named emails have the SuperAdmin as admin@crm.local: it becomes
    /// superadmin@crm.com.
    /// </summary>
    private async Task RenameLegacyAdminAsync()
    {
        if (await userManager.FindByEmailAsync(SuperAdminEmail) is not null
            || await userManager.FindByEmailAsync(DemoAccounts.LegacySuperAdminEmail) is not { } legacy
            || !await userManager.IsInRoleAsync(legacy, Roles.SuperAdmin))
        {
            return;
        }

        ThrowIfFailed(await userManager.SetUserNameAsync(legacy, SuperAdminEmail), "rename the legacy SuperAdmin user name");
        ThrowIfFailed(await userManager.SetEmailAsync(legacy, SuperAdminEmail), "rename the legacy SuperAdmin email");
        logger.LogInformation("Renamed the legacy SuperAdmin {Old} to {New}.", DemoAccounts.LegacySuperAdminEmail, SuperAdminEmail);
    }

    private async Task SeedDemoAccountsAsync()
    {
        var password = configuration["Seed:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:SuperAdminPassword is not configured; the demo accounts were not created.");
            return;
        }

        foreach (var account in DemoAccounts.All.Where(a => a.Role is not (Roles.SuperAdmin or DemoAccounts.CustomerRole)))
        {
            if (await userManager.FindByEmailAsync(account.Email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = account.Email,
                Email = account.Email,
                EmailConfirmed = true,
                FullName = DemoAccounts.FullNameOf(account.Role),
                IsActive = true,
                IsOnDuty = true,
            };
            ThrowIfFailed(await userManager.CreateAsync(user, password), $"create the demo user {account.Email}");
            ThrowIfFailed(await userManager.AddToRoleAsync(user, account.Role), $"add the {account.Role} role");
            logger.LogInformation("Seeded demo {Role} user {Email}.", account.Role, account.Email);
        }

        var exists = await db.Customers.IgnoreQueryFilters()
            .AnyAsync(c => c.Contacts.Any(x => x.Type == ContactType.Email && x.Value == DemoAccounts.CustomerEmail));
        if (!exists)
        {
            db.Customers.Add(Customer.Create(DemoAccounts.FullNameOf(DemoAccounts.CustomerRole), DemoAccounts.CustomerEmail, null,
                timeProvider.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo portal customer {Email}.", DemoAccounts.CustomerEmail);
        }
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
        if (await userManager.FindByEmailAsync(SuperAdminEmail) is not null
            || await userManager.FindByEmailAsync(DemoAccounts.LegacySuperAdminEmail) is { } legacy
               && await userManager.IsInRoleAsync(legacy, Roles.SuperAdmin))
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
