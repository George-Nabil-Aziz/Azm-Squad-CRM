using System.Text.Json;
using Crm.Application.Auth;
using Crm.Application.Settings;
using Crm.Domain.Integrations;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.DemoData;

/// <summary>
/// Development-only demo integrations (CRM-58..60): three API keys (one revoked) with scopes and last-used times, two webhooks
/// (one active, one disabled) with a delivery log of ten finished deliveries, and ERP links for <see cref="ErpLinkCount"/> demo
/// customers with sync log rows. Same switches as <see cref="DemoDataSeeder"/>; each group has its own marker (key names, webhook
/// URLs, ERP ids starting with <see cref="ErpIdPrefix"/>) so it never duplicates. Nothing leaves the machine: no delivery is left
/// Pending (the delivery job would send it), the plain API keys and the webhook secrets are generated and thrown away (only the
/// hash / the encrypted value is stored, so no demo key or secret can ever be used), and the ERP is never called.
/// </summary>
public sealed class DemoIntegrationsSeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    ISecretProtector protector,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DemoIntegrationsSeeder> logger)
{
    public const int ErpLinkCount = 10;
    public const string ErpIdPrefix = "ERP-DEMO-";
    private const string HookUrlPrefix = "https://example.com/hooks/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = ActivatorUtilities.CreateInstance<DemoIntegrationsSeeder>(services);
        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<DemoIntegrationsSeeder>>().LogError(exception, "Seeding the development demo integrations failed.");
        }
    }

    /// <summary>Returns the number of rows added.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(DemoDataSeeder.FlagKey))
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var owner = (await userManager.FindByEmailAsync(DemoAccounts.SuperAdminEmail))?.Id;
        var added = await AddApiKeysAsync(now, owner, cancellationToken)
                    + await AddWebhooksAsync(now, cancellationToken)
                    + await AddErpAsync(now, cancellationToken);
        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} development demo integration rows (API keys, webhooks, ERP).", added);
        }

        return added;
    }

    private async Task<int> AddApiKeysAsync(DateTime now, Guid? owner, CancellationToken cancellationToken)
    {
        string[] names = ["Website", "Mobile app", "Old reporting script"];
        if (await db.ApiKeys.AnyAsync(k => names.Contains(k.Name), cancellationToken))
        {
            return 0;
        }

        var website = ApiKey.Generate("Website", [ApiKeyScopes.TicketsWrite, ApiKeyScopes.CustomersRead, ApiKeyScopes.CustomersWrite], owner, now.AddDays(-45)).Key;
        website.MarkUsed(now.AddMinutes(-12));
        var mobile = ApiKey.Generate("Mobile app", [ApiKeyScopes.TicketsRead, ApiKeyScopes.TicketsWrite, ApiKeyScopes.CustomersRead], owner, now.AddDays(-20)).Key;
        mobile.MarkUsed(now.AddHours(-3));
        var old = ApiKey.Generate("Old reporting script", [ApiKeyScopes.TicketsRead], owner, now.AddDays(-90)).Key;
        old.MarkUsed(now.AddDays(-40));
        old.Revoke(now.AddDays(-30));
        db.ApiKeys.AddRange(website, mobile, old); // the plain keys were never kept: these keys cannot be used
        return 3;
    }

    private async Task<int> AddWebhooksAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (await db.Webhooks.AnyAsync(w => w.Url.StartsWith(HookUrlPrefix), cancellationToken))
        {
            return 0;
        }

        var active = Webhook.Create("CRM hooks", HookUrlPrefix + "crm", WebhookEvents.All, protector.Protect(Webhook.NewSecret()), now.AddDays(-14));
        var paused = Webhook.Create("Billing sync (paused)", HookUrlPrefix + "billing", [WebhookEvents.TicketResolved], protector.Protect(Webhook.NewSecret()), now.AddDays(-30));
        paused.SetEnabled(false, now.AddDays(-5));
        db.Webhooks.AddRange(active, paused);

        var recent = await db.Tickets.AsNoTracking().OrderByDescending(t => t.CreatedAt).Take(10).ToListAsync(cancellationToken);
        object Data(int i) => i < recent.Count
            ? new { ticketId = recent[i].Id, number = recent[i].DisplayNumber, subject = recent[i].Subject }
            : new { ticketId = Guid.NewGuid(), number = $"TCK-{1000 + i}", subject = $"Demo ticket {i + 1}" };

        // (webhook, event, minutes ago, failures before the end, delivered at the end)
        (Webhook Hook, string Event, int MinutesAgo, int Failures, bool Delivered)[] plan =
        [
            (active, WebhookEvents.TicketCreated, 25, 0, true),
            (active, WebhookEvents.TicketCreated, 140, 0, true),
            (active, WebhookEvents.TicketResolved, 300, 0, true),
            (active, WebhookEvents.TicketCreated, 700, 0, true),
            (active, WebhookEvents.TicketResolved, 1500, 0, true),
            (active, WebhookEvents.TicketCreated, 2600, 0, true),
            (active, WebhookEvents.TicketCreated, 420, 2, true), // two failed tries, then delivered
            (active, WebhookEvents.TicketResolved, 3100, WebhookBackoff.MaxAttempts, false), // every try failed: given up
            (paused, WebhookEvents.TicketResolved, 9000, 0, true),
            (paused, WebhookEvents.TicketResolved, 7300, WebhookBackoff.MaxAttempts, false),
        ];
        for (var i = 0; i < plan.Length; i++)
        {
            var (hook, eventName, minutesAgo, failures, delivered) = plan[i];
            var created = now.AddMinutes(-minutesAgo);
            var id = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new { id, @event = eventName, occurredAt = created, data = Data(i) }, Json);
            var delivery = WebhookDelivery.Create(id, hook.Id, eventName, payload, created);
            for (var attempt = 0; attempt < failures; attempt++)
            {
                delivery.RecordFailure(attempt % 2 == 0 ? 503 : null, attempt % 2 == 0 ? "HTTP 503 Service Unavailable" : "The request timed out.", created.AddMinutes(attempt));
            }

            if (delivered)
            {
                delivery.RecordSuccess(200, created.AddMinutes(failures));
            }

            db.WebhookDeliveries.Add(delivery);
        }

        return 2 + plan.Length;
    }

    private async Task<int> AddErpAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.ErpCustomerId != null && c.ErpCustomerId.StartsWith(ErpIdPrefix), cancellationToken))
        {
            return 0;
        }

        var customers = await db.Customers
            .Where(c => c.Email != null && c.Email.EndsWith(DemoDataCatalog.CustomerEmailDomain) && c.ErpCustomerId == null)
            .OrderBy(c => c.CreatedAt)
            .Take(ErpLinkCount)
            .ToListAsync(cancellationToken);
        var rows = 0;
        for (var i = 0; i < customers.Count; i++)
        {
            var erpId = $"{ErpIdPrefix}{10001 + i}";
            customers[i].LinkErp(erpId, now.AddDays(-12));
            rows++;
            // One log row per customer (the page was opened), the first two also had an earlier failed or not configured fetch.
            var at = now.AddHours(-(2 + (i * 7)));
            db.ErpSyncLogs.Add(i switch
            {
                3 => ErpSyncLog.Create(customers[i].Id, erpId, ErpSyncResult.Failed, "The ERP did not answer (timeout after 10 seconds).", at),
                7 => ErpSyncLog.Create(customers[i].Id, erpId, ErpSyncResult.Failed, "The ERP answered 502 Bad Gateway.", at),
                _ => ErpSyncLog.Create(customers[i].Id, erpId, ErpSyncResult.Success, null, at),
            });
            rows++;
            if (i < 2)
            {
                db.ErpSyncLogs.Add(ErpSyncLog.Create(customers[i].Id, erpId, ErpSyncResult.NotConfigured, "The ERP connection is not configured.", at.AddDays(-3)));
                rows++;
            }
        }

        return rows;
    }
}
