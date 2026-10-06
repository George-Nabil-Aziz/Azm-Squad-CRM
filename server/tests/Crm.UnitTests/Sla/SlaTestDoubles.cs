using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>In-memory SLA policy storage with the same contract as the EF Core repository (seeded with the defaults).</summary>
internal sealed class FakeSlaPolicyRepository : ISlaPolicyRepository
{
    public FakeSlaPolicyRepository(DateTime seededAt)
    {
        Policies = [.. SlaPolicy.Defaults.Select(d => SlaPolicy.Create(d.Priority, d.ResponseMinutes, d.ResolutionMinutes, seededAt))];
    }

    public List<SlaPolicy> Policies { get; }

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<SlaPolicy>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SlaPolicy> list = [.. Policies.OrderBy(p => p.Priority)];
        return Task.FromResult(list);
    }

    public Task<SlaPolicy?> FindAsync(TicketPriority priority, CancellationToken cancellationToken) =>
        Task.FromResult(Policies.FirstOrDefault(p => p.Priority == priority));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
