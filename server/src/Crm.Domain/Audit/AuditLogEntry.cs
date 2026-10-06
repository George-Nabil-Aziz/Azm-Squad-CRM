namespace Crm.Domain.Audit;

/// <summary>
/// One line of the audit log (CRM-34): who did what to which entity, with the old and new values, from which IP,
/// and when (UTC). Append-only: there is no way to change an entry after <see cref="Create"/>.
/// </summary>
public sealed class AuditLogEntry
{
    public const int ActionMaxLength = 64;
    public const int EntityTypeMaxLength = 64;
    public const int EntityIdMaxLength = 64;
    public const int EmailMaxLength = 256;
    public const int IpAddressMaxLength = 64;

    private AuditLogEntry()
    {
        // EF Core materializes entries through this constructor.
    }

    /// <summary>Identity column: orders entries of the same time.</summary>
    public long Id { get; private set; }

    public DateTime OccurredAt { get; private set; }

    /// <summary>The acting user; null when nobody was signed in and none could be identified (unknown email at login).</summary>
    public Guid? UserId { get; private set; }

    /// <summary>The email typed at a login attempt; null for signed-in actions (the email is read from the user).</summary>
    public string? UserEmail { get; private set; }

    /// <summary>One of <see cref="AuditActions"/>, e.g. "user.created".</summary>
    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    /// <summary>JSON of the values before the change (null for creations and events without values).</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON of the values after the change.</summary>
    public string? NewValues { get; private set; }

    public string? IpAddress { get; private set; }

    public static AuditLogEntry Create(
        DateTime utcNow,
        Guid? userId,
        string? userEmail,
        string action,
        string entityType,
        string? entityId,
        string? oldValues,
        string? newValues,
        string? ipAddress)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        return new AuditLogEntry
        {
            OccurredAt = utcNow,
            UserId = userId,
            UserEmail = Limit(userEmail, EmailMaxLength),
            Action = Limit(action, ActionMaxLength)!,
            EntityType = Limit(entityType, EntityTypeMaxLength)!,
            EntityId = Limit(entityId, EntityIdMaxLength),
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = Limit(ipAddress, IpAddressMaxLength),
        };
    }

    private static string? Limit(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

/// <summary>The audited actions (values are stored and shown; never rename one, only add).</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "login.succeeded";
    public const string LoginFailed = "login.failed";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string SlaPolicyUpdated = "sla-policy.updated";
    public const string CustomerDeleted = "customer.deleted";
    public const string CustomerContactRemoved = "customer-contact.removed";

    public static IReadOnlyList<string> All { get; } =
    [
        LoginSucceeded, LoginFailed,
        UserCreated, UserUpdated, UserDeactivated, UserReactivated,
        SlaPolicyUpdated,
        CustomerDeleted, CustomerContactRemoved,
    ];
}
