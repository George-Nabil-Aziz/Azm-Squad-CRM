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

    /// <summary>
    /// The policy that applies to a new ticket (CRM-61): the department override of this priority when the department has one,
    /// else the global policy. Not tracked; null only when no global policy exists.
    /// </summary>
    Task<SlaPolicy?> FindEffectiveAsync(TicketPriority priority, Guid? departmentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
