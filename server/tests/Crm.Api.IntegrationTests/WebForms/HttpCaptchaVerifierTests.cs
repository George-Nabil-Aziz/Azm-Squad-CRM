using System.Net;
using Crm.Application.WebForms;
using Crm.Infrastructure.WebForms;

namespace Crm.Api.IntegrationTests.WebForms;

/// <summary>CRM-55: the captcha provider call (no network: the HTTP handler is a stub).</summary>
public class HttpCaptchaVerifierTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Content { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Content = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static HttpCaptchaVerifier Verifier(StubHandler handler, string? secret = "secret") =>
        new(new HttpClient(handler), new WebFormOptions { CaptchaSecret = secret, CaptchaVerifyUrl = "https://captcha.example/siteverify" });

    [Fact]
    public async Task SuccessTrue_IsAccepted_AndSendsTheSecretTokenAndIp()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":true}");

        var ok = await Verifier(handler).VerifyAsync("tok", "1.2.3.4", CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("https://captcha.example/siteverify", handler.Request!.RequestUri!.ToString());
        Assert.Contains("secret=secret", handler.Content);
        Assert.Contains("response=tok", handler.Content);
        Assert.Contains("remoteip=1.2.3.4", handler.Content);
    }

    [Fact]
    public async Task SuccessFalse_IsRefused()
    {
        Assert.False(await Verifier(new StubHandler(HttpStatusCode.OK, "{\"success\":false}")).VerifyAsync("tok", null, CancellationToken.None));
    }

    [Fact]
    public async Task AnHttpError_IsRefused()
    {
        Assert.False(await Verifier(new StubHandler(HttpStatusCode.BadGateway, "oops")).VerifyAsync("tok", null, CancellationToken.None));
    }

    [Fact]
    public async Task AMissingToken_IsRefused_WhenACaptchaIsConfigured()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":true}");

        Assert.False(await Verifier(handler).VerifyAsync(" ", null, CancellationToken.None));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task WithoutASecret_TheCaptchaIsSkipped()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":false}");

        Assert.True(await Verifier(handler, secret: null).VerifyAsync(null, null, CancellationToken.None));
        Assert.Null(handler.Request);
    }
}
