using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Users;
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
    IValidator<UpdateUserRequest> updateValidator) : IUserService
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

        var roles = await RolesByUserAsync(pageUsers.Select(u => u.Id).ToList(), cancellationToken);
        var items = pageUsers
            .Select(u => ToResponse(u, roles.TryGetValue(u.Id, out var userRoles) ? userRoles : []))
            .ToList();
        return new PagedResult<UserResponse>(items, page, pageSize, totalCount);
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        return ToResponse(user, [.. await userManager.GetRolesAsync(user)]);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var roles = request.Roles!.Distinct(StringComparer.Ordinal).ToList();
        EnsureMayManage(roles);

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
        };

        // One transaction: a user is never left without roles.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ThrowIfFailed(await userManager.CreateAsync(user, request.Password!));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles));
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(user, roles);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var user = await FindAsync(id);
        var currentRoles = await userManager.GetRolesAsync(user);
        var roles = request.Roles!.Distinct(StringComparer.Ordinal).ToList();
        EnsureMayManage(currentRoles);
        EnsureMayManage(roles);

        var email = request.Email!.Trim();
        var owner = await userManager.FindByEmailAsync(email);
        if (owner is not null && owner.Id != user.Id)
        {
            throw EmailTaken();
        }

        user.Email = email;
        user.UserName = email;
        user.FullName = request.FullName!.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // UpdateAsync also refreshes NormalizedEmail / NormalizedUserName.
        ThrowIfFailed(await userManager.UpdateAsync(user));
        ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles)));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles.Except(currentRoles)));
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(user, roles);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        if (user.Id == currentUser.UserId)
        {
            throw new ConflictException(UserText.CannotDeactivateSelf);
        }

        EnsureMayManage(await userManager.GetRolesAsync(user));
        await SetActiveAsync(user, false);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        EnsureMayManage(await userManager.GetRolesAsync(user));
        await SetActiveAsync(user, true);
    }

    private async Task SetActiveAsync(ApplicationUser user, bool isActive)
    {
        if (user.IsActive == isActive)
        {
            return; // Idempotent: deactivating an inactive user (or reactivating an active one) changes nothing.
        }

        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));
    }

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

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");

    private static UserResponse ToResponse(ApplicationUser user, IReadOnlyList<string> roles) =>
        new(user.Id, user.Email!, user.FullName, [.. roles.Order(StringComparer.Ordinal)], user.IsActive);
}
