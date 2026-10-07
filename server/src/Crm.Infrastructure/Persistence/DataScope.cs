using Crm.Application.Auth;
using Crm.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

/// <summary>The per-request data scope (CRM-61): a mutable holder, filled once by <see cref="DataScopeLoader"/>; unrestricted until then.</summary>
public sealed class DataScope : IDataScope
{
    public bool RestrictDepartments { get; private set; }

    public IReadOnlyList<Guid> DepartmentIds { get; private set; } = [];

    public bool RestrictBranch { get; private set; }

    public Guid? BranchId { get; private set; }

    public void RestrictToBranch(Guid branchId)
    {
        RestrictBranch = true;
        BranchId = branchId;
    }

    public void RestrictToDepartments(IEnumerable<Guid> departmentIds)
    {
        RestrictDepartments = true;
        DepartmentIds = [.. departmentIds];
    }
}

/// <summary>Works out the scope of the signed-in staff user. Called by the API middleware after authentication.</summary>
public interface IDataScopeLoader
{
    Task LoadAsync(CancellationToken cancellationToken);
}

public sealed class DataScopeLoader(CrmDbContext db, ICurrentUser currentUser, DataScope scope) : IDataScopeLoader
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return; // anonymous requests, the portal and jobs are not restricted
        }

        // CRM-62: a user (other than SuperAdmin) with a branch works inside it.
        if (!currentUser.IsInRole(Roles.SuperAdmin)
            && await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.BranchId).FirstOrDefaultAsync(cancellationToken) is { } branchId)
        {
            scope.RestrictToBranch(branchId);
        }

        // A user who is only an Agent works inside their departments; Supervisor, Admin and SuperAdmin see every department.
        var agentOnly = currentUser.IsInRole(Roles.Agent)
                        && !currentUser.IsInRole(Roles.Supervisor) && !currentUser.IsInRole(Roles.Admin) && !currentUser.IsInRole(Roles.SuperAdmin);
        if (!agentOnly)
        {
            return;
        }

        var departments = await db.UserDepartments.AsNoTracking()
            .Where(m => m.UserId == userId).Select(m => m.DepartmentId).ToListAsync(cancellationToken);
        scope.RestrictToDepartments(departments);
    }
}
