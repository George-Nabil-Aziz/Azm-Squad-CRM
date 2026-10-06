using System.Globalization;
using Crm.Domain.Settings;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Settings;

/// <summary>The keys of the stored system settings and which of them are secret (CRM-35).</summary>
public static class SettingKeys
{
    public const string BusinessHoursEnabled = "business-hours.enabled";
    public const string BusinessDays = "business-hours.days";
    public const string BusinessStart = "business-hours.start";
    public const string BusinessEnd = "business-hours.end";
    public const string TimeZone = "general.time-zone";
    public const string TicketPrefix = "tickets.prefix";

    public const string EmailFromAddress = "email.from-address";
    public const string EmailFromName = "email.from-name";
    public const string SmtpHost = "email.smtp.host";
    public const string SmtpPort = "email.smtp.port";
    public const string SmtpSecurity = "email.smtp.security";
    public const string SmtpUserName = "email.smtp.user-name";
    public const string SmtpPassword = "email.smtp.password";
    public const string ImapHost = "email.imap.host";
    public const string ImapPort = "email.imap.port";
    public const string ImapSecurity = "email.imap.security";
    public const string ImapUserName = "email.imap.user-name";
    public const string ImapPassword = "email.imap.password";
    public const string ImapFolder = "email.imap.folder";

    public const string WhatsAppPhoneNumberId = "whatsapp.phone-number-id";
    public const string WhatsAppAccessToken = "whatsapp.access-token";
    public const string WhatsAppAppSecret = "whatsapp.app-secret";
    public const string WhatsAppVerifyToken = "whatsapp.verify-token";

    /// <summary>Settings whose value is encrypted and never returned by the API.</summary>
    public static IReadOnlySet<string> Secrets { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SmtpPassword, ImapPassword, WhatsAppAccessToken, WhatsAppAppSecret, WhatsAppVerifyToken,
    };
}

/// <summary>The stored settings with their defaults, read through one place (secrets are decrypted only on request).</summary>
public sealed class SettingsSnapshot(IEnumerable<SystemSetting> stored, ISecretProtector protector)
{
    public const string DefaultTimeZone = "Asia/Riyadh";
    public const string DefaultStart = "08:00";
    public const string DefaultEnd = "16:00";

    /// <summary>Sunday to Thursday (the Saudi working week).</summary>
    public static IReadOnlyList<string> DefaultDays { get; } = ["sunday", "monday", "tuesday", "wednesday", "thursday"];

    public static IReadOnlyList<string> DayNames { get; } =
        ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    private readonly Dictionary<string, SystemSetting> _stored = stored.ToDictionary(s => s.Key, StringComparer.Ordinal);

    public bool BusinessHoursEnabled => Text(SettingKeys.BusinessHoursEnabled) == "true";

    public IReadOnlyList<string> BusinessDays =>
        Text(SettingKeys.BusinessDays) is { } days
            ? [.. days.Split(',', StringSplitOptions.RemoveEmptyEntries)]
            : DefaultDays;

    public string BusinessStart => Text(SettingKeys.BusinessStart) ?? DefaultStart;

    public string BusinessEnd => Text(SettingKeys.BusinessEnd) ?? DefaultEnd;

    public string TimeZone => Text(SettingKeys.TimeZone) ?? DefaultTimeZone;

    public string TicketPrefix => Text(SettingKeys.TicketPrefix) ?? Ticket.NumberPrefix;

    /// <summary>The stored text of a non-secret setting, or null.</summary>
    public string? Text(string key) => _stored.TryGetValue(key, out var setting) ? setting.Value : null;

    public int? Number(string key) =>
        int.TryParse(Text(key), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>True when the secret has a value (without decrypting it).</summary>
    public bool HasSecret(string key) => _stored.TryGetValue(key, out var setting) && setting.Value is not null;

    /// <summary>The decrypted secret; null when not set or unreadable.</summary>
    public string? Secret(string key) =>
        _stored.TryGetValue(key, out var setting) && setting.Value is { } value ? protector.Unprotect(value) : null;

    /// <summary>The calendar of the business hours, or null when they are off (or stored values are unusable): the SLA clock then runs 24/7.</summary>
    public BusinessCalendar? BuildCalendar()
    {
        if (!BusinessHoursEnabled
            || !TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var zone)
            || !TimeOnly.TryParseExact(BusinessStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !TimeOnly.TryParseExact(BusinessEnd, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            || start >= end)
        {
            return null;
        }

        var days = BusinessDays
            .Select(name => Array.IndexOf(DayNames.ToArray(), name))
            .Where(index => index >= 0)
            .Select(index => (DayOfWeek)index)
            .ToList();
        return days.Count == 0 ? null : new BusinessCalendar(days, start, end, zone);
    }
}
