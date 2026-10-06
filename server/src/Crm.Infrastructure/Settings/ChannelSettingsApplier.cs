using Crm.Application.Channels;
using Crm.Application.Settings;
using Microsoft.Extensions.Configuration;

namespace Crm.Infrastructure.Settings;

/// <summary>
/// Puts the channel credentials saved in the system settings into the live <see cref="EmailChannelOptions"/> /
/// <see cref="WhatsAppChannelOptions"/> singletons. Every stored value overrides its configured counterpart
/// (appsettings, user-secrets, environment); a cleared setting falls back to the configuration. Other API instances
/// pick a change up at their next start.
/// </summary>
public sealed class ChannelSettingsApplier(
    EmailChannelOptions email,
    WhatsAppChannelOptions whatsApp,
    IConfiguration configuration,
    ISettingsRepository repository,
    ISecretProtector protector) : IChannelSettingsApplier
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var s = new SettingsSnapshot(await repository.ListAsync(cancellationToken), protector);

        var emailBase = configuration.GetSection(EmailChannelOptions.SectionName).Get<EmailChannelOptions>() ?? new EmailChannelOptions();
        email.FromAddress = s.Text(SettingKeys.EmailFromAddress) ?? emailBase.FromAddress;
        email.FromName = s.Text(SettingKeys.EmailFromName) ?? emailBase.FromName;
        email.Smtp.Host = s.Text(SettingKeys.SmtpHost) ?? emailBase.Smtp.Host;
        email.Smtp.Port = s.Number(SettingKeys.SmtpPort) ?? emailBase.Smtp.Port;
        email.Smtp.Security = s.Text(SettingKeys.SmtpSecurity) ?? emailBase.Smtp.Security;
        email.Smtp.UserName = s.Text(SettingKeys.SmtpUserName) ?? emailBase.Smtp.UserName;
        email.Smtp.Password = s.Secret(SettingKeys.SmtpPassword) ?? emailBase.Smtp.Password;
        email.Imap.Host = s.Text(SettingKeys.ImapHost) ?? emailBase.Imap.Host;
        email.Imap.Port = s.Number(SettingKeys.ImapPort) ?? emailBase.Imap.Port;
        email.Imap.Security = s.Text(SettingKeys.ImapSecurity) ?? emailBase.Imap.Security;
        email.Imap.UserName = s.Text(SettingKeys.ImapUserName) ?? emailBase.Imap.UserName;
        email.Imap.Password = s.Secret(SettingKeys.ImapPassword) ?? emailBase.Imap.Password;
        email.Imap.Folder = s.Text(SettingKeys.ImapFolder) ?? emailBase.Imap.Folder;
        email.Imap.BatchSize = emailBase.Imap.BatchSize;

        var whatsAppBase = configuration.GetSection(WhatsAppChannelOptions.SectionName).Get<WhatsAppChannelOptions>()
                           ?? new WhatsAppChannelOptions();
        whatsApp.PhoneNumberId = s.Text(SettingKeys.WhatsAppPhoneNumberId) ?? whatsAppBase.PhoneNumberId;
        whatsApp.AccessToken = s.Secret(SettingKeys.WhatsAppAccessToken) ?? whatsAppBase.AccessToken;
        whatsApp.AppSecret = s.Secret(SettingKeys.WhatsAppAppSecret) ?? whatsAppBase.AppSecret;
        whatsApp.VerifyToken = s.Secret(SettingKeys.WhatsAppVerifyToken) ?? whatsAppBase.VerifyToken;
        whatsApp.ApiBaseUrl = whatsAppBase.ApiBaseUrl;
        whatsApp.TemplateLanguage = whatsAppBase.TemplateLanguage;
    }
}
