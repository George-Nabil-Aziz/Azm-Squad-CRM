using Crm.Application.Common.Security;
using Crm.Application.Departments;
using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Departments;

/// <summary>In-memory department storage with the same contract as the EF Core repository.</summary>
internal sealed class FakeDepartmentRepository : IDepartmentRepository
{
    public List<Department> Departments { get; } = [];

    public List<DepartmentSlaPolicy> SlaPolicies { get; } = [];

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Department>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        IReadOnlyList<Department> list = [.. Departments.Where(d => !activeOnly || d.IsActive).OrderBy(d => d.Name)];
        return Task.FromResult(list);
    }

    public Task<Department?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.FirstOrDefault(d => d.Id == id));

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.Any(d => d.NormalizedName == normalizedName && d.Id != exceptId));

    public void Add(Department department) => Departments.Add(department);

    public Task<IReadOnlyList<DepartmentSlaPolicy>> ListSlaPoliciesAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        IReadOnlyList<DepartmentSlaPolicy> list = [.. SlaPolicies.Where(p => p.DepartmentId == departmentId).OrderBy(p => p.Priority)];
        return Task.FromResult(list);
    }

    public Task<DepartmentSlaPolicy?> FindSlaPolicyAsync(Guid departmentId, TicketPriority priority, CancellationToken cancellationToken) =>
        Task.FromResult(SlaPolicies.FirstOrDefault(p => p.DepartmentId == departmentId && p.Priority == priority));

    public void AddSlaPolicy(DepartmentSlaPolicy policy) => SlaPolicies.Add(policy);

    public void RemoveSlaPolicy(DepartmentSlaPolicy policy) => SlaPolicies.Remove(policy);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>The data scope of a unit test (a restricted agent or an unrestricted user).</summary>
internal sealed class FakeDataScope(bool restrictDepartments = false, params Guid[] departmentIds) : IDataScope
{
    public bool RestrictDepartments { get; } = restrictDepartments;

    public IReadOnlyList<Guid> DepartmentIds { get; } = departmentIds;
}
