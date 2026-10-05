namespace Crm.Application.Common.Security;

/// <summary>The signed-in user of the current request (implemented in Crm.Api from the JWT claims).</summary>
public interface ICurrentUser
{
    /// <summary>The user's id (<c>sub</c> claim), or null when the request is anonymous.</summary>
    Guid? UserId { get; }

    bool IsInRole(string role);
}
