namespace Crm.Domain.Common;

/// <summary>
/// An entity that is never physically deleted: deleting it sets <see cref="IsDeleted"/> and the row stays, so rows
/// that point at it (tickets, notes, ...) stay valid. Crm.Infrastructure hides deleted rows with the global query
/// filter named "SoftDelete" (a test fails for an ISoftDeletable entity without it).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    /// <summary>When the entity was deleted (UTC); null while it is not deleted.</summary>
    DateTime? DeletedAt { get; }
}
