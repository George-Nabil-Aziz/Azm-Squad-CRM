using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>
/// A customer that tickets belong to. <see cref="Email"/> and <see cref="Phone"/> are the customer's primary contact
/// details (CRM-9 adds more contacts per customer). Times are UTC and come from the caller (the Application layer
/// passes the injected TimeProvider's time), so the rules are testable without a clock or a database.
/// </summary>
public sealed class Customer : ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;

    private Customer()
    {
        // EF Core materializes customers through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    /// <summary>A new customer. Name is required; empty email / phone are stored as null; values are trimmed.</summary>
    public static Customer Create(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var customer = new Customer { Id = Guid.NewGuid(), CreatedAt = utcNow };
        customer.SetProfile(name, email, phone, utcNow);
        return customer;
    }

    /// <summary>Replaces the profile (same rules as <see cref="Create"/>). A deleted customer cannot be changed.</summary>
    public void Update(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted customer cannot be changed.");
        }

        SetProfile(name, email, phone, utcNow);
    }

    /// <summary>Soft delete: the row and its data stay (tickets keep pointing at it). Deleting twice changes nothing.</summary>
    public void Delete(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        UpdatedAt = utcNow;
    }

    private void SetProfile(string name, string? email, string? phone, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Email = TrimToNull(email);
        Phone = TrimToNull(phone);
        UpdatedAt = utcNow;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
