using Crm.Domain.Sla;

namespace Crm.Application.Sla;

/// <summary>Counts of what one run of <see cref="SlaMonitorJob"/> flagged.</summary>
public sealed record SlaMonitorResult(int ResponseBreaches, int ResolutionBreaches);

/// <summary>
/// The recurring SLA job (CRM-21, every minute): flags late tickets and records one event per ticket and kind. A plain
/// class (no Hangfire reference); Hangfire only calls <see cref="RunAsync"/>, tests call it directly.
/// </summary>
public sealed class SlaMonitorJob(ITicketSlaRepository repository, TimeProvider clock)
{
    /// <summary>Tickets handled per run; the rest follow in the next minute.</summary>
    public const int BatchSize = 500;

    public async Task<SlaMonitorResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await repository.ListBreachCandidatesAsync(now, BatchSize, cancellationToken);

        int response = 0, resolution = 0;
        foreach (var ticket in candidates)
        {
            if (ticket.IsResponseBreachedAt(now) && ticket.MarkResponseBreached())
            {
                repository.AddEvent(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResponseBreached, 0, ticket.ResponseDueAt, now));
                response++;
            }

            if (ticket.IsResolutionBreachedAt(now) && ticket.MarkResolutionBreached())
            {
                repository.AddEvent(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResolutionBreached, 0, ticket.ResolutionDueAt, now));
                resolution++;
            }
        }

        if (response + resolution == 0)
        {
            return new SlaMonitorResult(0, 0);
        }

        return await repository.SaveChangesAsync(cancellationToken) ? new SlaMonitorResult(response, resolution) : new SlaMonitorResult(0, 0);
    }
}
