using Crm.Application.Common.Paging;

namespace Crm.Application.Users;

/// <summary>
/// Staff user management (the API allows it only to SuperAdmin and Admin: policy <c>ManageUsers</c>).
/// Failures: <c>ValidationException</c> 400 (invalid data, email already in use), <c>NotFoundException</c> 404,
/// <c>ForbiddenException</c> 403 (SuperAdmin rule), <c>ConflictException</c> 409 (deactivating yourself).
/// </summary>
public interface IUserService
{
    Task<PagedResult<UserResponse>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken);

    Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
