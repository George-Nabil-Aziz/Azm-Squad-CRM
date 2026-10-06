namespace Crm.Domain.Tickets;

/// <summary>
/// A ticket category managed by admins. Names are unique ignoring case and surrounding spaces
/// (<see cref="NormalizedName"/>). Categories are never deleted: an inactive category cannot be chosen for new tickets
/// but stays on the tickets that already use it. Times are UTC and come from the caller.
/// </summary>
public sealed class TicketCategory
{
    public const int NameMaxLength = 100;

    private TicketCategory()
    {
        // EF Core materializes categories through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-case trimmed name: the unique key that makes "Billing" and " billing " the same category.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>A new, active category. The name is required and trimmed.</summary>
    public static TicketCategory Create(string name, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var category = new TicketCategory { Id = Guid.NewGuid(), CreatedAt = utcNow, IsActive = true };
        category.SetName(name);
        category.UpdatedAt = utcNow;
        return category;
    }

    /// <summary>Renames and activates / deactivates the category.</summary>
    public void Update(string name, bool isActive, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        SetName(name);
        IsActive = isActive;
        UpdatedAt = utcNow;
    }

    /// <summary>The form compared for uniqueness: trimmed, upper case (invariant culture).</summary>
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        NormalizedName = NormalizeName(name);
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
