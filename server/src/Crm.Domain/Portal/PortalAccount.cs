namespace Crm.Domain.Portal;

/// <summary>
/// The sign-in identity of a customer in the portal (CRM-40): one per email, linked to the customer that has this email
/// (found by email, or created on the first sign-in). Customers are not staff users and have no password.
/// </summary>
public sealed class PortalAccount
{
    private PortalAccount()
    {
        // EF Core materializes accounts through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Trimmed, lower case; unique.</summary>
    public string Email { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public static PortalAccount Create(Guid customerId, string email, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        EnsureUtc(utcNow);
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("An account needs a customer.", nameof(customerId));
        }

        return new PortalAccount { Id = Guid.NewGuid(), CustomerId = customerId, Email = email.Trim().ToLowerInvariant(), CreatedAt = utcNow };
    }

    /// <summary>Points the account at another customer (the customer it was linked to is gone).</summary>
    public void LinkTo(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("An account needs a customer.", nameof(customerId));
        }

        CustomerId = customerId;
    }

    public void RecordLogin(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        LastLoginAt = utcNow;
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
