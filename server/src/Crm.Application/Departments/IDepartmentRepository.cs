using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Departments;

/// <summary>Department storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IDepartmentRepository
{
    /// <summary>Every department (or only the active ones), ordered by name. Not tracked.</summary>
    Task<IReadOnlyList<Department>> ListAsync(bool activeOnly, CancellationToken cancellationToken);

    /// <summary>The department (tracked), or null.</summary>
    Task<Department?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when another department (not <paramref name="exceptId"/>) has this normalized name.</summary>
    Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    void Add(Department department);

    /// <summary>The SLA overrides of one department, High to Low. Not tracked.</summary>
    Task<IReadOnlyList<DepartmentSlaPolicy>> ListSlaPoliciesAsync(Guid departmentId, CancellationToken cancellationToken);

    /// <summary>The override of a department and priority (tracked), or null.</summary>
    Task<DepartmentSlaPolicy?> FindSlaPolicyAsync(Guid departmentId, TicketPriority priority, CancellationToken cancellationToken);

    void AddSlaPolicy(DepartmentSlaPolicy policy);

    void RemoveSlaPolicy(DepartmentSlaPolicy policy);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
