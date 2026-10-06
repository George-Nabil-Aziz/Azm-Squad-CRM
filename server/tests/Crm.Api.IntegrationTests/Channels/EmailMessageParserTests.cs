using Crm.Domain.Channels;
using Crm.Infrastructure.Channels.Email;
using MimeKit;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-24: an email (MimeKit) becomes a channel-neutral inbound message.</summary>
public class EmailMessageParserTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    private static MimeMessage Mail(string? messageId = "m1@mail.example", MimeEntity? body = null)
    {
        var mail = new MimeMessage { Subject = "Re: Printer broken [TKT-000004]", Body = body ?? new TextPart("plain") { Text = "  It still does not print.  " } };
        mail.From.Add(new MailboxAddress("Nour Ali", "Nour.Ali@Example.com"));
        mail.To.Add(new MailboxAddress("Support", "support@azm.example"));
        mail.Date = new DateTimeOffset(2026, 10, 6, 7, 55, 0, TimeSpan.FromHours(3));
        if (messageId is null)
        {
            mail.Headers.Remove(HeaderId.MessageId);
        }
        else
        {
            mail.MessageId = messageId;
        }

        return mail;
    }

    [Fact]
    public void Parse_ReadsIdSenderSubjectAndText()
    {
        var parsed = EmailMessageParser.Parse(Mail(), Now)!;

        Assert.Equal(ChannelKind.Email, parsed.Channel);
        Assert.Equal("m1@mail.example", parsed.ExternalId);
        Assert.Equal("nour.ali@example.com", parsed.From);
        Assert.Equal("Nour Ali", parsed.FromName);
        Assert.Equal("Re: Printer broken [TKT-000004]", parsed.Subject);
        Assert.Equal("It still does not print.", parsed.Body);
        Assert.Equal(Now, parsed.ReceivedAt);
    }

    [Fact]
    public void Parse_HtmlOnlyBody_BecomesText()
    {
        var html = new TextPart("html") { Text = "<html><body><p>Hello&nbsp;team,</p><p>It is <b>broken</b>.<br>Thanks</p></body></html>" };

        var parsed = EmailMessageParser.Parse(Mail(body: html), Now)!;

        Assert.Equal("Hello team,\nIt is broken.\nThanks", parsed.Body);
    }

    [Fact]
    public void Parse_WithoutMessageId_UsesAStableHash()
    {
        var first = EmailMessageParser.Parse(Mail(messageId: null), Now)!;
        var second = EmailMessageParser.Parse(Mail(messageId: null), Now.AddMinutes(5))!;

        Assert.StartsWith("sha256:", first.ExternalId, StringComparison.Ordinal);
        Assert.Equal(first.ExternalId, second.ExternalId);
    }

    [Fact]
    public void Parse_WithoutSender_ReturnsNull()
    {
        var mail = new MimeMessage { Subject = "x", Body = new TextPart("plain") { Text = "x" } };

        Assert.Null(EmailMessageParser.Parse(mail, Now));
    }
}
