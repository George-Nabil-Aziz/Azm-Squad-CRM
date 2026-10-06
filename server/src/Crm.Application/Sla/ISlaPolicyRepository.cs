using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

/// <summary>SLA policy storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ISlaPolicyRepository
{
    /// <summary>Every policy, High → Low. Not tracked.</summary>
    Task<IReadOnlyList<SlaPolicy>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The policy of a priority (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<SlaPolicy?> FindAsync(TicketPriority priority, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
