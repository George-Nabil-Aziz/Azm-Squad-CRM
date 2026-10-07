using Crm.Application.Branding;
using Crm.Application.Channels;
using Crm.Infrastructure.Channels.Email;
using MimeKit;

namespace Crm.Api.IntegrationTests.Branding;

/// <summary>CRM-63 AC 4: emails carry the branding (plain text is always kept).</summary>
public class BrandedEmailTests
{
    private static string Html(MimeMessage message) => message.HtmlBody ?? string.Empty;

    [Fact]
    public void WithoutBranding_TheBodyStaysPlainText()
    {
        var body = BrandedEmail.Build("Hello\nWorld", null);

        var text = Assert.IsType<TextPart>(body);
        Assert.True(text.IsPlain);
        Assert.Equal("Hello\nWorld", text.Text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WithNothingConfigured_TheBodyStaysPlainText()
    {
        var body = BrandedEmail.Build("Hello", new EmailBranding(null, null, null, null));

        Assert.IsType<TextPart>(body);
    }

    [Fact]
    public void WithColors_ThereIsAnHtmlPartWithTheColor_AndTheTextIsEscaped()
    {
        var message = new MimeMessage { Body = BrandedEmail.Build("Hi <b>there</b>\nBye", new EmailBranding("#112233", "#445566", null, null)) };

        Assert.Equal("Hi <b>there</b>\nBye", message.TextBody!.ReplaceLineEndings("\n"));
        Assert.Contains("#112233", Html(message));
        Assert.Contains("Hi &lt;b&gt;there&lt;/b&gt;<br", Html(message));
        Assert.DoesNotContain("<b>there</b>", Html(message));
    }

    [Fact]
    public void WithALogo_ItIsEmbeddedByContentId()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        var message = new MimeMessage { Body = BrandedEmail.Build("Hi", new EmailBranding("#112233", null, png, "image/png")) };

        Assert.Contains("cid:", Html(message));
        var image = Assert.Single(message.BodyParts.OfType<MimePart>(), p => p.ContentType.MimeType == "image/png");
        Assert.NotNull(image.ContentId);
        Assert.Contains($"cid:{image.ContentId}", Html(message));
    }

    [Fact]
    public void UnsafeColors_AreNeverWrittenIntoTheHtml()
    {
        var message = new MimeMessage { Body = BrandedEmail.Build("Hi", new EmailBranding("red;\"><script>", null, null, null)) };

        Assert.DoesNotContain("<script>", Html(message));
    }

    private sealed class FakeBranding(EmailBranding branding) : IBrandingService
    {
        public Task<BrandingResponse> GetAsync(CancellationToken c) => throw new NotSupportedException();
        public Task<BrandingResponse> UpdateAsync(UpdateBrandingRequest r, CancellationToken c) => throw new NotSupportedException();
        public Task<BrandingResponse> UploadLogoAsync(UploadLogoRequest r, CancellationToken c) => throw new NotSupportedException();
        public Task<BrandingResponse> RemoveLogoAsync(CancellationToken c) => throw new NotSupportedException();
        public Task<BrandingLogo?> OpenLogoAsync(CancellationToken c) => throw new NotSupportedException();
        public Task<EmailBranding> GetEmailBrandingAsync(CancellationToken c) => Task.FromResult(branding);
    }

    private sealed class CapturingTransport : ISmtpTransport
    {
        public List<MimeMessage> Sent { get; } = [];

        public Task SendAsync(MimeMessage message, EmailChannelOptions.SmtpSettings settings, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task TheEmailChannel_SendsBrandedMail_WhenBrandingIsSet()
    {
        var options = new EmailChannelOptions { FromAddress = "support@azm.example", Smtp = { Host = "smtp.example.test", Port = 587 } };
        var transport = new CapturingTransport();
        var provider = new SmtpEmailProvider(options, transport, new FakeBranding(new EmailBranding("#0a5cad", null, null, null)));

        var result = await provider.SendAsync(new OutboundChannelMessage(Guid.NewGuid(), "nour@example.com", "Re: Hi", "We are on it.", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        var mail = Assert.Single(transport.Sent);
        Assert.Contains("#0a5cad", mail.HtmlBody);
        Assert.Equal("We are on it.", mail.TextBody!.TrimEnd());
    }
}
