namespace Crm.Application.Auth;

/// <summary>
/// Checked on every authenticated request (JWT <c>OnTokenValidated</c>): a token of a deactivated or deleted
/// user stops working immediately instead of at its expiry.
/// </summary>
public interface IActiveUserChecker
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken);
}
