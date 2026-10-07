using Crm.Application.Audit;
using Crm.Application.Auth;
using Crm.Application.Branches;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Departments;
using Crm.Application.Users;
using Crm.Domain.Audit;
using Crm.Domain.Departments;
using Crm.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Infrastructure.Identity;

/// <summary>User management on ASP.NET Identity (<see cref="UserManager{TUser}"/>) and <see cref="CrmDbContext"/>.</summary>
public sealed class UserService(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IValidator<ListUsersQuery> listValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IAuditLogger audit) : IUserService
{
    private const string LikeEscape = "\\";

    public async Task<PagedResult<UserResponse>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        var page = query.Page ?? PagingDefaults.DefaultPage;
        var pageSize = query.PageSize ?? PagingDefaults.DefaultPageSize;

        var users = db.Users.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE is case-insensitive on SQL Server (default collation) and on SQLite (ASCII); % and _ are escaped.
            var pattern = $"%{EscapeLike(search)}%";
            users = users.Where(u => EF.Functions.Like(u.FullName, pattern, LikeEscape)
                                     || EF.Functions.Like(u.Email!, pattern, LikeEscape));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var pageUsers = await users
            .OrderBy(u => u.FullName).ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var pageIds = pageUsers.Select(u => u.Id).ToList();
        var roles = await RolesByUserAsync(pageIds, cancellationToken);
        var departments = await DepartmentsByUserAsync(pageIds, cancellationToken);
        var items = pageUsers
            .Select(u => ToResponse(u, roles.TryGetValue(u.Id, out var userRoles) ? userRoles : [],
                departments.TryGetValue(u.Id, out var userDepartments) ? userDepartments : []))
            .ToList();
        return new PagedResult<UserResponse>(items, page, pageSize, totalCount);
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        return ToResponse(user, [.. await userManager.GetRolesAsync(user)], await DepartmentIdsAsync(user.Id, cancellationToken));
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var roles = request.Roles!.Distinct(StringComparer.Ordinal).ToList();
        EnsureMayManage(roles);
        var departmentIds = await ValidDepartmentIdsAsync(request.DepartmentIds, cancellationToken);
        if (request.BranchId is not null)
        {
            await EnsureMayAssignBranchAsync(request.BranchId, cancellationToken);
        }

        var email = request.Email!.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw EmailTaken();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName!.Trim(),
            BranchId = request.BranchId,
        };

        // One transaction: a user is never left without roles.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ThrowIfFailed(await userManager.CreateAsync(user, request.Password!));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles));
        await SetDepartmentsAsync(user.Id, departmentIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.UserCreated, "User", user.Id.ToString(), NewValues: Snapshot(user, roles)),
            cancellationToken);

        return ToResponse(user, roles, departmentIds ?? []);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var user = await FindAsync(id);
        var currentRoles = await userManager.GetRolesAsync(user);
        var roles = request.Roles!.Distinct(StringComparer.Ordinal).ToList();
        EnsureMayManage(currentRoles);
        EnsureMayManage(roles);
        var departmentIds = await ValidDepartmentIdsAsync(request.DepartmentIds, cancellationToken);

        var email = request.Email!.Trim();
        var owner = await userManager.FindByEmailAsync(email);
        if (owner is not null && owner.Id != user.Id)
        {
            throw EmailTaken();
        }

        var oldValues = Snapshot(user, currentRoles);
        user.Email = email;
        user.UserName = email;
        user.FullName = request.FullName!.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // UpdateAsync also refreshes NormalizedEmail / NormalizedUserName.
        ThrowIfFailed(await userManager.UpdateAsync(user));
        ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles)));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles.Except(currentRoles)));
        if (departmentIds is not null)
        {
            await SetDepartmentsAsync(user.Id, departmentIds, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.UserUpdated, "User", user.Id.ToString(), oldValues, Snapshot(user, roles)),
            cancellationToken);

        return ToResponse(user, roles, await DepartmentIdsAsync(user.Id, cancellationToken));
    }

    public async Task<UserResponse> SetBranchAsync(Guid id, SetUserBranchRequest request, CancellationToken cancellationToken)
    {
        EnsureMayAssignBranch();
        var user = await FindAsync(id);
        await EnsureBranchExistsAsync(request.BranchId, cancellationToken);

        var old = user.BranchId;
        user.BranchId = request.BranchId;
        ThrowIfFailed(await userManager.UpdateAsync(user));
        await audit.LogAsync(
            new AuditEvent(AuditActions.UserUpdated, "User", user.Id.ToString(), new { branchId = old }, new { branchId = user.BranchId }),
            cancellationToken);
        return ToResponse(user, [.. await userManager.GetRolesAsync(user)], await DepartmentIdsAsync(user.Id, cancellationToken));
    }

    /// <summary>CRM-62: only a user with branches.manage may put users in a branch (a branch user must not lift their own restriction).</summary>
    private void EnsureMayAssignBranch()
    {
        if (!currentUser.HasPermission(Permissions.BranchesManage))
        {
            throw new ForbiddenException(UserText.SuperAdminOnly);
        }
    }

    private async Task EnsureMayAssignBranchAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureMayAssignBranch();
        await EnsureBranchExistsAsync(branchId, cancellationToken);
    }

    private async Task EnsureBranchExistsAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        if (branchId is { } id && !await db.Branches.AnyAsync(b => b.Id == id && b.IsActive, cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["branchId"] = [BranchText.Unavailable] });
        }
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        if (user.Id == currentUser.UserId)
        {
            throw new ConflictException(UserText.CannotDeactivateSelf);
        }

        EnsureMayManage(await userManager.GetRolesAsync(user));
        await SetActiveAsync(user, false, cancellationToken);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        EnsureMayManage(await userManager.GetRolesAsync(user));
        await SetActiveAsync(user, true, cancellationToken);
    }

    private async Task SetActiveAsync(ApplicationUser user, bool isActive, CancellationToken cancellationToken)
    {
        if (user.IsActive == isActive)
        {
            return; // Idempotent: deactivating an inactive user (or reactivating an active one) changes nothing.
        }

        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));
        await audit.LogAsync(
            new AuditEvent(isActive ? AuditActions.UserReactivated : AuditActions.UserDeactivated, "User", user.Id.ToString(),
                new { isActive = !isActive }, new { isActive }),
            cancellationToken);
    }

    /// <summary>What the audit log keeps of a user: never the password or its hash.</summary>
    private static object Snapshot(ApplicationUser user, IEnumerable<string> roles) =>
        new { email = user.Email, fullName = user.FullName, roles = roles.Order(StringComparer.Ordinal).ToArray() };

    private async Task<ApplicationUser> FindAsync(Guid id) =>
        await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException(UserText.NotFound);

    private async Task<Dictionary<Guid, List<string>>> RolesByUserAsync(
        List<Guid> userIds, CancellationToken cancellationToken)
    {
        var pairs = await db.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(db.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.RoleName).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Only a user with <see cref="Permissions.UsersManageSuperAdmins"/> (SuperAdmin) may give the SuperAdmin role or
    /// change a SuperAdmin (no privilege escalation by an Admin).
    /// </summary>
    private void EnsureMayManage(IEnumerable<string> roles)
    {
        if (roles.Contains(Roles.SuperAdmin) && !currentUser.HasPermission(Permissions.UsersManageSuperAdmins))
        {
            throw new ForbiddenException(UserText.SuperAdminOnly);
        }
    }

    private static ValidationException EmailTaken() =>
        new(new Dictionary<string, string[]> { ["email"] = [UserText.EmailTaken] });

    /// <summary>Identity rejected the change (rules the validators do not cover, e.g. characters in the user name): 400.</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email")
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
        throw new ValidationException(errors);
    }

    private async Task<List<Guid>> DepartmentIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserDepartments.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.DepartmentId).ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, List<Guid>>> DepartmentsByUserAsync(List<Guid> userIds, CancellationToken cancellationToken) =>
        (await db.UserDepartments.AsNoTracking().Where(m => userIds.Contains(m.UserId)).ToListAsync(cancellationToken))
        .GroupBy(m => m.UserId)
        .ToDictionary(group => group.Key, group => group.Select(m => m.DepartmentId).ToList());

    /// <summary>CRM-61: null stays null (unchanged); otherwise the distinct ids, every one must be an existing department (400 on departmentIds).</summary>
    private async Task<List<Guid>?> ValidDepartmentIdsAsync(IReadOnlyList<Guid>? ids, CancellationToken cancellationToken)
    {
        if (ids is null)
        {
            return null;
        }

        var distinct = ids.Distinct().ToList();
        var known = await db.Departments.CountAsync(d => distinct.Contains(d.Id), cancellationToken);
        if (known != distinct.Count)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["departmentIds"] = [DepartmentText.UnknownDepartments] });
        }

        return distinct;
    }

    private async Task SetDepartmentsAsync(Guid userId, List<Guid>? departmentIds, CancellationToken cancellationToken)
    {
        if (departmentIds is null)
        {
            return;
        }

        var current = await db.UserDepartments.Where(m => m.UserId == userId).ToListAsync(cancellationToken);
        db.UserDepartments.RemoveRange(current.Where(m => !departmentIds.Contains(m.DepartmentId)));
        db.UserDepartments.AddRange(departmentIds.Except(current.Select(m => m.DepartmentId)).Select(id => new UserDepartment(userId, id)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");

    private static UserResponse ToResponse(ApplicationUser user, IReadOnlyList<string> roles, IReadOnlyList<Guid> departmentIds) =>
        new(user.Id, user.Email!, user.FullName, [.. roles.Order(StringComparer.Ordinal)], user.IsActive, [.. departmentIds.Order()], user.BranchId);
}
