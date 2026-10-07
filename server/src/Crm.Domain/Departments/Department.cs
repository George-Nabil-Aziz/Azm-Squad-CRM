namespace Crm.Domain.Departments;

/// <summary>
/// A team that owns tickets (CRM-61). Names are unique ignoring case and surrounding spaces (<see cref="NormalizedName"/>).
/// Departments are never deleted: an inactive one cannot be chosen for tickets but stays on the tickets that use it.
/// Times are UTC and come from the caller.
/// </summary>
public sealed class Department
{
    public const int NameMaxLength = 100;

    private Department()
    {
        // EF Core materializes departments through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-case trimmed name: the unique key.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>A new, active department. The name is required and trimmed.</summary>
    public static Department Create(string name, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var department = new Department { Id = Guid.NewGuid(), CreatedAt = utcNow, IsActive = true };
        department.SetName(name);
        department.UpdatedAt = utcNow;
        return department;
    }

    /// <summary>Renames and activates / deactivates the department.</summary>
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
