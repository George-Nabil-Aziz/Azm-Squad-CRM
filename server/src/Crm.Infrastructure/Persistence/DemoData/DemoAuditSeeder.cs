using System.Text.Json;
using Crm.Application.Auth;
using Crm.Domain.Audit;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.DemoData;

/// <summary>
/// Development-only demo audit-log entries (CRM-34): sign-ins, failed sign-ins, a role change, a user created and deactivated,
/// SLA changes, a customer deletion, a settings and a branding change, spread over the last days with local and public-looking
/// IP addresses, so the audit page has varied examples. Same switches as <see cref="DemoDataSeeder"/>; runs only while the log
/// holds fewer than <see cref="NearlyEmpty"/> entries, so it adds the examples once and never again. The entries are written
/// directly (<see cref="AuditLogEntry.Create"/> with past times and addresses; <c>IAuditLogger</c> always stamps "now" and the
/// current request's IP); values use the same JSON shapes the real services write. Nothing is sent.
/// </summary>
public sealed class DemoAuditSeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DemoAuditSeeder> logger)
{
    public const int NearlyEmpty = 10;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = ActivatorUtilities.CreateInstance<DemoAuditSeeder>(services);
        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<DemoAuditSeeder>>().LogError(exception, "Seeding the development demo audit log failed.");
        }
    }

    /// <summary>Returns the number of entries added.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(DemoDataSeeder.FlagKey))
        {
            return 0;
        }

        if (await db.AuditLog.CountAsync(cancellationToken) >= NearlyEmpty)
        {
            return 0;
        }

        var superAdmin = await userManager.FindByEmailAsync(DemoAccounts.SuperAdminEmail) ?? await userManager.FindByEmailAsync(DemoAccounts.AdminEmail);
        if (superAdmin is null)
        {
            return 0;
        }

        var admin = await userManager.FindByEmailAsync(DemoAccounts.AdminEmail) ?? superAdmin;
        var agent = await userManager.FindByEmailAsync(DemoAccounts.AgentEmail) ?? superAdmin;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var customerId = Guid.NewGuid().ToString();
        var newUserId = Guid.NewGuid().ToString();

        // (minutes ago, user, typed email for sign-ins, action, entity type, entity id, old, new, ip)
        var rows = new (int Ago, ApplicationUser? User, string? Typed, string Action, string Entity, string? EntityId, object? Old, object? New, string Ip)[]
        {
            (8, superAdmin, null, AuditActions.LoginSucceeded, "User", superAdmin.Id.ToString(), null, null, "::1"),
            (35, null, "ghost@example.com", AuditActions.LoginFailed, "User", null, null, new { reason = "unknown-user" }, "203.0.113.50"),
            (95, agent, agent.Email, AuditActions.LoginSucceeded, "User", agent.Id.ToString(), null, null, "192.168.1.24"),
            (180, agent, agent.Email, AuditActions.LoginFailed, "User", agent.Id.ToString(), null, new { reason = "wrong-password" }, "192.168.1.24"),
            (260, admin, null, AuditActions.UserUpdated, "User", agent.Id.ToString(),
                new { email = agent.Email, fullName = agent.FullName, roles = new[] { "Agent" } },
                new { email = agent.Email, fullName = agent.FullName, roles = new[] { "Supervisor" } }, "::1"),
            (420, admin, null, AuditActions.UserCreated, "User", newUserId, null,
                new { email = "nour@crm.com", fullName = "Nour Hassan", roles = new[] { "Agent" } }, "192.168.1.10"),
            (600, admin, null, AuditActions.UserDeactivated, "User", newUserId, new { isActive = true }, new { isActive = false }, "192.168.1.10"),
            (840, superAdmin, null, AuditActions.SlaPolicyUpdated, "SlaPolicy", "High",
                new { responseMinutes = 60, resolutionMinutes = 240 }, new { responseMinutes = 30, resolutionMinutes = 240 }, "::1"),
            (900, superAdmin, null, AuditActions.SlaPolicyUpdated, "SlaPolicy", "Low",
                new { responseMinutes = 480, resolutionMinutes = 2880 }, new { responseMinutes = 480, resolutionMinutes = 1440 }, "::1"),
            (1500, admin, null, AuditActions.CustomerDeleted, "Customer", customerId,
                new { name = "Acme Trading", email = "info@acme.example", phone = "+966500000000" }, null, "198.51.100.23"),
            (1700, admin, null, AuditActions.CustomerContactRemoved, "CustomerContact", Guid.NewGuid().ToString(),
                new { customerId, type = "whatsapp", value = "+966511111111" }, null, "198.51.100.23"),
            (2200, superAdmin, null, AuditActions.SettingsUpdated, "SystemSettings", null,
                new { ticketPrefix = "TCK-", timeZone = "Asia/Riyadh", businessHours = new { enabled = false, start = "08:00", end = "17:00" } },
                new
                {
                    response = new { ticketPrefix = "SUP-", timeZone = "Asia/Riyadh", businessHours = new { enabled = true, start = "09:00", end = "17:00" } },
                    secretsChanged = Array.Empty<string>(),
                }, "::1"),
            (2900, superAdmin, null, AuditActions.BrandingUpdated, "Branding", null,
                new { primaryColor = "#1d4ed8", secondaryColor = "#64748b" }, new { primaryColor = "#0f766e", secondaryColor = "#64748b" }, "::1"),
            (4300, superAdmin, null, AuditActions.LoginSucceeded, "User", superAdmin.Id.ToString(), null, null, "::1"),
        };

        foreach (var row in rows)
        {
            db.AuditLog.Add(AuditLogEntry.Create(
                now.AddMinutes(-row.Ago),
                row.User?.Id,
                row.Typed,
                row.Action,
                row.Entity,
                row.EntityId,
                row.Old is null ? null : JsonSerializer.Serialize(row.Old, Json),
                row.New is null ? null : JsonSerializer.Serialize(row.New, Json),
                row.Ip));
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} development demo audit log entries.", rows.Length);
        return rows.Length;
    }
}
