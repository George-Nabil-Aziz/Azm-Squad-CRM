using Crm.Application.Departments;
using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Departments;

/// <summary>EF Core storage of departments and their SLA overrides.</summary>
public sealed class DepartmentRepository(CrmDbContext db) : IDepartmentRepository
{
    public async Task<IReadOnlyList<Department>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.Departments.AsNoTracking()
            .Where(d => !activeOnly || d.IsActive)
            .OrderBy(d => d.Name).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);

    public Task<Department?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(d => d.NormalizedName == normalizedName && d.Id != exceptId, cancellationToken);

    public void Add(Department department) => db.Departments.Add(department);

    public async Task<IReadOnlyList<DepartmentSlaPolicy>> ListSlaPoliciesAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var list = await db.DepartmentSlaPolicies.AsNoTracking().Where(p => p.DepartmentId == departmentId).ToListAsync(cancellationToken);
        return [.. list.OrderBy(p => p.Priority)]; // enum order (High to Low) in memory: the column holds the name
    }

    public Task<DepartmentSlaPolicy?> FindSlaPolicyAsync(Guid departmentId, TicketPriority priority, CancellationToken cancellationToken) =>
        db.DepartmentSlaPolicies.FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.Priority == priority, cancellationToken);

    public void AddSlaPolicy(DepartmentSlaPolicy policy) => db.DepartmentSlaPolicies.Add(policy);

    public void RemoveSlaPolicy(DepartmentSlaPolicy policy) => db.DepartmentSlaPolicies.Remove(policy);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
