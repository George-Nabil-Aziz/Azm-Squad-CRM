namespace Crm.Domain.Settings;

/// <summary>A global setting stored as a key / value pair (CRM-27: <see cref="AutoAssignEnabledKey"/>).</summary>
public sealed class AppSetting
{
    public const string AutoAssignEnabledKey = "assignment.auto-enabled";
    public const int KeyMaxLength = 100;
    public const int ValueMaxLength = 1000;

    private AppSetting()
    {
        // EF Core materializes settings through this constructor.
    }

    public AppSetting(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Value = value;
    }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
