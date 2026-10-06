using System.Globalization;
using Crm.Application.Audit;
using Crm.Application.Common.Validation;
using Crm.Domain.Audit;
using Crm.Domain.Settings;
using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Settings;

/// <summary>
/// System settings use cases: validation, encryption of secrets, storage through <see cref="ISettingsRepository"/>, the
/// audit entry and applying the channel credentials to the live options. Secrets are never part of any response or
/// audit entry.
/// </summary>
public sealed class SystemSettingsService(
    ISettingsRepository repository,
    ISecretProtector protector,
    IChannelSettingsApplier channelApplier,
    IAuditLogger audit,
    TimeProvider timeProvider,
    IValidator<UpdateSettingsRequest> validator) : ISystemSettingsService
{
    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken) =>
        ToResponse(new SettingsSnapshot(await repository.ListAsync(cancellationToken), protector));

    public async Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);

        var stored = (await repository.ListAsync(cancellationToken)).ToDictionary(s => s.Key, StringComparer.Ordinal);
        var oldResponse = ToResponse(new SettingsSnapshot(stored.Values, protector));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        void Set(string key, string? value)
        {
            if (stored.TryGetValue(key, out var existing))
            {
                existing.SetValue(value, now);
                return;
            }

            var created = SystemSetting.Create(key, SettingKeys.Secrets.Contains(key), value, now);
            stored[key] = created;
            repository.Add(created);
        }

        if (request.BusinessHours is { } hours)
        {
            Set(SettingKeys.BusinessHoursEnabled, hours.Enabled == true ? "true" : "false");
            Set(SettingKeys.BusinessDays, string.Join(',', hours.Days ?? []));
            Set(SettingKeys.BusinessStart, hours.Start);
            Set(SettingKeys.BusinessEnd, hours.End);
        }

        if (request.TimeZone is { } timeZone)
        {
            Set(SettingKeys.TimeZone, timeZone.Trim());
        }

        if (request.TicketPrefix is { } prefix)
        {
            Set(SettingKeys.TicketPrefix, Ticket.NormalizePrefix(prefix));
        }

        if (request.Email is { } email)
        {
            Set(SettingKeys.EmailFromAddress, Clean(email.FromAddress));
            Set(SettingKeys.EmailFromName, Clean(email.FromName));
            Set(SettingKeys.SmtpHost, Clean(email.SmtpHost));
            Set(SettingKeys.SmtpPort, Number(email.SmtpPort));
            Set(SettingKeys.SmtpSecurity, Clean(email.SmtpSecurity));
            Set(SettingKeys.SmtpUserName, Clean(email.SmtpUserName));
            Set(SettingKeys.ImapHost, Clean(email.ImapHost));
            Set(SettingKeys.ImapPort, Number(email.ImapPort));
            Set(SettingKeys.ImapSecurity, Clean(email.ImapSecurity));
            Set(SettingKeys.ImapUserName, Clean(email.ImapUserName));
            Set(SettingKeys.ImapFolder, Clean(email.ImapFolder));
        }

        if (request.WhatsApp is { } whatsApp)
        {
            Set(SettingKeys.WhatsAppPhoneNumberId, Clean(whatsApp.PhoneNumberId));
        }

        var changedSecrets = new List<string>();
        if (request.Secrets is { } secrets)
        {
            SetSecret(nameof(SettingsSecrets.SmtpPassword), SettingKeys.SmtpPassword, secrets.SmtpPassword);
            SetSecret(nameof(SettingsSecrets.ImapPassword), SettingKeys.ImapPassword, secrets.ImapPassword);
            SetSecret(nameof(SettingsSecrets.WhatsAppAccessToken), SettingKeys.WhatsAppAccessToken, secrets.WhatsAppAccessToken);
            SetSecret(nameof(SettingsSecrets.WhatsAppAppSecret), SettingKeys.WhatsAppAppSecret, secrets.WhatsAppAppSecret);
            SetSecret(nameof(SettingsSecrets.WhatsAppVerifyToken), SettingKeys.WhatsAppVerifyToken, secrets.WhatsAppVerifyToken);
        }

        void SetSecret(string name, string key, string? plainText)
        {
            if (plainText is null)
            {
                return; // keep
            }

            Set(key, plainText.Length == 0 ? null : protector.Protect(plainText));
            changedSecrets.Add(char.ToLowerInvariant(name[0]) + name[1..]);
        }

        await repository.SaveChangesAsync(cancellationToken);
        await channelApplier.ApplyAsync(cancellationToken);

        var response = ToResponse(new SettingsSnapshot(stored.Values, protector));
        await audit.LogAsync(
            new AuditEvent(AuditActions.SettingsUpdated, "SystemSettings", null, oldResponse, new { response, secretsChanged = changedSecrets }),
            cancellationToken);
        return response;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Number(int? number) => number?.ToString(CultureInfo.InvariantCulture);

    private static SettingsResponse ToResponse(SettingsSnapshot s) => new(
        new BusinessHoursSettings(s.BusinessHoursEnabled, s.BusinessDays, s.BusinessStart, s.BusinessEnd),
        s.TimeZone,
        s.TicketPrefix,
        new EmailSettings(
            s.Text(SettingKeys.EmailFromAddress), s.Text(SettingKeys.EmailFromName),
            s.Text(SettingKeys.SmtpHost), s.Number(SettingKeys.SmtpPort) ?? 587, s.Text(SettingKeys.SmtpSecurity) ?? "StartTls",
            s.Text(SettingKeys.SmtpUserName),
            s.Text(SettingKeys.ImapHost), s.Number(SettingKeys.ImapPort) ?? 993, s.Text(SettingKeys.ImapSecurity) ?? "SslOnConnect",
            s.Text(SettingKeys.ImapUserName), s.Text(SettingKeys.ImapFolder) ?? "INBOX"),
        new WhatsAppSettings(s.Text(SettingKeys.WhatsAppPhoneNumberId)),
        new SecretsSet(
            s.HasSecret(SettingKeys.SmtpPassword), s.HasSecret(SettingKeys.ImapPassword), s.HasSecret(SettingKeys.WhatsAppAccessToken),
            s.HasSecret(SettingKeys.WhatsAppAppSecret), s.HasSecret(SettingKeys.WhatsAppVerifyToken)));
}

public sealed class SystemSettingsProvider(ISettingsRepository repository, ISecretProtector protector) : ISystemSettingsProvider
{
    public async Task<RuntimeSettings> GetAsync(CancellationToken cancellationToken)
    {
        var snapshot = new SettingsSnapshot(await repository.ListAsync(cancellationToken), protector);
        return new RuntimeSettings(snapshot.BuildCalendar(), snapshot.TicketPrefix);
    }
}
