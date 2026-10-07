namespace Crm.Domain.Branches;

/// <summary>
/// A branch (office) of the company (CRM-62). Names are unique ignoring case and surrounding spaces (<see cref="NormalizedName"/>).
/// Branches are never deleted: an inactive one cannot be chosen for new users and customers but stays on the rows that use it.
/// Times are UTC and come from the caller.
/// </summary>
public sealed class Branch
{
    public const int NameMaxLength = 100;

    private Branch()
    {
        // EF Core materializes branches through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-case trimmed name: the unique key.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>A new, active branch. The name is required and trimmed.</summary>
    public static Branch Create(string name, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var branch = new Branch { Id = Guid.NewGuid(), CreatedAt = utcNow, IsActive = true };
        branch.SetName(name);
        branch.UpdatedAt = utcNow;
        return branch;
    }

    /// <summary>Renames and activates / deactivates the branch.</summary>
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
