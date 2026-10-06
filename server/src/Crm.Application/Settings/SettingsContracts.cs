using Crm.Domain.Sla;
using Crm.Domain.Settings;

namespace Crm.Application.Settings;

/// <summary>Business hours: day names are "sunday".."saturday", times are "HH:mm" in the configured time zone.</summary>
public sealed record BusinessHoursSettings(bool? Enabled, IReadOnlyList<string>? Days, string? Start, string? End);

public sealed record EmailSettings(
    string? FromAddress,
    string? FromName,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpSecurity,
    string? SmtpUserName,
    string? ImapHost,
    int? ImapPort,
    string? ImapSecurity,
    string? ImapUserName,
    string? ImapFolder);

public sealed record WhatsAppSettings(string? PhoneNumberId);

/// <summary>
/// Secrets a PUT may carry: <c>null</c> keeps the stored value, <c>""</c> clears it, any other text replaces it.
/// They are stored encrypted and never returned (see <see cref="SecretsSet"/>).
/// </summary>
public sealed record SettingsSecrets(
    string? SmtpPassword, string? ImapPassword, string? WhatsAppAccessToken, string? WhatsAppAppSecret, string? WhatsAppVerifyToken);

/// <summary>Which secrets have a value. The values themselves are never returned.</summary>
public sealed record SecretsSet(
    bool SmtpPassword, bool ImapPassword, bool WhatsAppAccessToken, bool WhatsAppAppSecret, bool WhatsAppVerifyToken);

/// <summary>
/// PUT /api/settings. A <c>null</c> section (or time zone / prefix) keeps what is stored; inside a section every field is
/// applied as sent (an empty text clears an optional setting).
/// </summary>
public sealed record UpdateSettingsRequest(
    BusinessHoursSettings? BusinessHours,
    string? TimeZone,
    string? TicketPrefix,
    EmailSettings? Email,
    WhatsAppSettings? WhatsApp,
    SettingsSecrets? Secrets);

public sealed record SettingsResponse(
    BusinessHoursSettings BusinessHours,
    string TimeZone,
    string TicketPrefix,
    EmailSettings Email,
    WhatsAppSettings WhatsApp,
    SecretsSet SecretsSet);

public interface ISystemSettingsService
{
    Task<SettingsResponse> GetAsync(CancellationToken cancellationToken);

    Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken);
}

/// <summary>What ticket creation needs (read on every new ticket, so a change applies to the next ticket).</summary>
public sealed record RuntimeSettings(BusinessCalendar? Calendar, string TicketPrefix);

public interface ISystemSettingsProvider
{
    Task<RuntimeSettings> GetAsync(CancellationToken cancellationToken);
}

public interface ISettingsRepository
{
    /// <summary>Every stored setting (tracked, so changes are saved by <see cref="SaveChangesAsync"/>).</summary>
    Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken);

    void Add(SystemSetting setting);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Encrypts secrets before they are stored (implemented in Crm.Infrastructure with ASP.NET Data Protection).</summary>
public interface ISecretProtector
{
    string Protect(string plainText);

    /// <summary>The plain text, or null when it cannot be read (e.g. the encryption keys are gone).</summary>
    string? Unprotect(string protectedText);
}

/// <summary>Copies the stored channel credentials into the live channel options (stored values override configuration).</summary>
public interface IChannelSettingsApplier
{
    Task ApplyAsync(CancellationToken cancellationToken);
}
