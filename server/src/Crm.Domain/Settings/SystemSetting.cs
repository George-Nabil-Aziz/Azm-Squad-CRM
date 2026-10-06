namespace Crm.Domain.Settings;

/// <summary>
/// One system setting (CRM-35): a key and its text value. A secret setting keeps its value already encrypted
/// (the Application layer protects it before calling <see cref="SetValue"/>); the Domain never sees the plain text.
/// </summary>
public sealed class SystemSetting
{
    public const int KeyMaxLength = 64;

    private SystemSetting()
    {
        // EF Core materializes settings through this constructor.
    }

    public string Key { get; private set; } = string.Empty;

    /// <summary>The value (encrypted for secrets); null = not set.</summary>
    public string? Value { get; private set; }

    public bool IsSecret { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static SystemSetting Create(string key, bool isSecret, string? value, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var setting = new SystemSetting { Key = key, IsSecret = isSecret };
        setting.SetValue(value, utcNow);
        return setting;
    }

    public void SetValue(string? value, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        Value = string.IsNullOrEmpty(value) ? null : value;
        UpdatedAt = utcNow;
    }
}
