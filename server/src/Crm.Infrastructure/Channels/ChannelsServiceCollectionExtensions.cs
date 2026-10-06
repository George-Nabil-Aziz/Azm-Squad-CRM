using Crm.Application.Channels;
using Crm.Infrastructure.Channels.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Channels;

public static class ChannelsServiceCollectionExtensions
{
    /// <summary>
    /// Email (SMTP / IMAP via MailKit) and WhatsApp (Cloud API) channels. Settings are read lazily from the final
    /// configuration (<c>Channels:Email</c>, <c>Channels:WhatsApp</c>); missing settings only make a channel
    /// "not configured", they never stop the app.
    /// </summary>
    public static IServiceCollection AddChannels(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            provider.GetRequiredService<IConfiguration>().GetSection(EmailChannelOptions.SectionName).Get<EmailChannelOptions>()
            ?? new EmailChannelOptions());

        services.AddSingleton(provider =>
            provider.GetRequiredService<IConfiguration>().GetSection(WhatsAppChannelOptions.SectionName).Get<WhatsAppChannelOptions>()
            ?? new WhatsAppChannelOptions());

        services.AddSingleton<ISmtpTransport, MailKitSmtpTransport>();
        services.AddScoped<IChannelProvider, SmtpEmailProvider>();
        services.AddScoped<IOutboundMessageRepository, OutboundMessageRepository>();

        services.AddSingleton<IImapMailbox, MailKitImapMailbox>();
        services.AddScoped<EmailInboxPoller>();
        services.AddScoped<IReceivedMessageRepository, ReceivedMessageRepository>();
        return services;
    }
}
