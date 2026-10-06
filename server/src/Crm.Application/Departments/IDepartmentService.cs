using Crm.Application.Sla;

namespace Crm.Application.Departments;

/// <summary>
/// Departments and their SLA overrides (reads need <c>tickets.view</c>, writes <c>departments.manage</c>, overrides
/// <c>sla.manage</c>, enforced by the API). Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404.
/// </summary>
public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentResponse>> ListAsync(ListDepartmentsQuery query, CancellationToken cancellationToken);

    Task<DepartmentResponse> CreateAsync(DepartmentRequest request, CancellationToken cancellationToken);

    Task<DepartmentResponse> UpdateAsync(Guid id, DepartmentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentSlaPolicyResponse>> ListSlaPoliciesAsync(Guid departmentId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the override of a priority ("high" / "mid" / "low").</summary>
    Task<DepartmentSlaPolicyResponse> SetSlaPolicyAsync(
        Guid departmentId, string priority, UpdateSlaPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>Removes the override (the global policy applies again); nothing happens when there is none.</summary>
    Task RemoveSlaPolicyAsync(Guid departmentId, string priority, CancellationToken cancellationToken);
}
