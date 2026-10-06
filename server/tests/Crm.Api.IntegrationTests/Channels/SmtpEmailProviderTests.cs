using Crm.Application.Channels;
using Crm.Infrastructure.Channels.Email;
using MailKit.Net.Smtp;
using MimeKit;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-23: the SMTP provider builds the email and reports failures (no network: the transport is a fake).</summary>
public class SmtpEmailProviderTests
{
    private static EmailChannelOptions ConfiguredOptions() => new()
    {
        FromAddress = "support@azm.example",
        FromName = "AZM Support",
        Smtp = { Host = "smtp.example.test", Port = 587 },
    };

    private sealed class FakeSmtpTransport : ISmtpTransport
    {
        public List<MimeMessage> Sent { get; } = [];

        public Exception? Throw { get; set; }

        public Task SendAsync(MimeMessage message, EmailChannelOptions.SmtpSettings settings, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                return Task.FromException(Throw);
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static OutboundChannelMessage Message() =>
        new(Guid.NewGuid(), "nour@example.com", "Re: Printer broken [TKT-000001]", "We are on it.\nRegards", null);

    [Fact]
    public async Task Send_BuildsTheMessage_WithSubjectTagAndRecipient()
    {
        var transport = new FakeSmtpTransport();
        var provider = new SmtpEmailProvider(ConfiguredOptions(), transport);
        var message = Message();

        var result = await provider.SendAsync(message, CancellationToken.None);

        Assert.True(result.Succeeded);
        var mail = Assert.Single(transport.Sent);
        Assert.Equal("nour@example.com", Assert.Single(mail.To.Mailboxes).Address);
        var from = Assert.Single(mail.From.Mailboxes);
        Assert.Equal(("support@azm.example", "AZM Support"), (from.Address, from.Name));
        Assert.Equal("Re: Printer broken [TKT-000001]", mail.Subject);
        Assert.Contains("We are on it.", mail.TextBody, StringComparison.Ordinal);
        Assert.Equal($"{message.Id:N}@azm.example", mail.MessageId);
        Assert.Equal(mail.MessageId, result.ProviderMessageId);
    }

    [Fact]
    public async Task Send_WhenTheTransportThrows_ReturnsFailure()
    {
        var transport = new FakeSmtpTransport { Throw = new SmtpProtocolException("The SMTP server has unexpectedly disconnected.") };
        var provider = new SmtpEmailProvider(ConfiguredOptions(), transport);

        var result = await provider.SendAsync(Message(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("The SMTP server has unexpectedly disconnected.", result.Error);
    }

    [Fact]
    public async Task Send_WhenNotConfigured_ReturnsNotConfigured()
    {
        var transport = new FakeSmtpTransport();
        var provider = new SmtpEmailProvider(new EmailChannelOptions(), transport);

        var result = await provider.SendAsync(Message(), CancellationToken.None);

        Assert.False(provider.IsConfigured);
        Assert.False(result.Succeeded);
        Assert.Empty(transport.Sent);
    }
}
