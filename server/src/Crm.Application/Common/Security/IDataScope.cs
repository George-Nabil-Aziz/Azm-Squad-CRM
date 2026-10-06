namespace Crm.Application.Common.Security;

/// <summary>
/// What the signed-in staff user may see (CRM-61): loaded once per request by the API and applied by EF Core query
/// filters, so every read of a ticket outside the scope behaves as "not found". Requests without a staff user (jobs, channel
/// inbound, the portal) are not restricted.
/// </summary>
public interface IDataScope
{
    /// <summary>True for a user whose roles are only Agent: they see tickets of <see cref="DepartmentIds"/> and tickets without department.</summary>
    bool RestrictDepartments { get; }

    /// <summary>The departments the restricted user belongs to.</summary>
    IReadOnlyList<Guid> DepartmentIds { get; }
}
