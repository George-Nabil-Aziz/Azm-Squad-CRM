using Crm.Application.Integrations;
using Crm.Domain.Integrations;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Integrations;

/// <summary>EF Core storage of webhooks and their deliveries (CRM-59).</summary>
public sealed class WebhookRepository(CrmDbContext db) : IWebhookRepository
{
    public void Add(Webhook webhook) => db.Webhooks.Add(webhook);

    public void Remove(Webhook webhook) => db.Webhooks.Remove(webhook);

    public void AddDelivery(WebhookDelivery delivery) => db.WebhookDeliveries.Add(delivery);

    public Task<Webhook?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Webhooks.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Webhook>> ListAsync(CancellationToken cancellationToken) =>
        await db.Webhooks.AsNoTracking().OrderBy(w => w.Name).ThenBy(w => w.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Webhook>> ListEnabledForEventAsync(string eventName, CancellationToken cancellationToken)
    {
        // Events is a short comma separated list; the exact match happens in memory.
        var enabled = await db.Webhooks.AsNoTracking().Where(w => w.IsEnabled && w.Events.Contains(eventName)).ToListAsync(cancellationToken);
        return [.. enabled.Where(w => w.IsSubscribedTo(eventName))];
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ListDeliveriesAsync(Guid webhookId, int take, CancellationToken cancellationToken) =>
        await db.WebhookDeliveries.AsNoTracking().Where(d => d.WebhookId == webhookId)
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WebhookDeliveryWork>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken)
    {
        var due = await db.WebhookDeliveries.Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= utcNow)
            .OrderBy(d => d.NextAttemptAt).ThenBy(d => d.Id).Take(take).ToListAsync(cancellationToken);
        var ids = due.Select(d => d.WebhookId).Distinct().ToList();
        var byId = await db.Webhooks.Where(w => ids.Contains(w.Id)).ToDictionaryAsync(w => w.Id, cancellationToken);
        return [.. due.Select(d => new WebhookDeliveryWork(d, byId.GetValueOrDefault(d.WebhookId)))];
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ListPendingAsync(Guid webhookId, CancellationToken cancellationToken) =>
        await db.WebhookDeliveries.Where(d => d.WebhookId == webhookId && d.Status == WebhookDeliveryStatus.Pending).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
