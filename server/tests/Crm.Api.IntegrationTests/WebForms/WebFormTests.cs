using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Portal;
using Crm.Application.WebForms;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.WebForms;

/// <summary>CRM-55: the public web form endpoint.</summary>
public class WebFormTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string Path = "/api/public/web-forms";

    private sealed class FakeCaptcha : ICaptchaVerifier
    {
        public bool Result { get; set; } = true;

        public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
            Task.FromResult(Result && token == "good");
    }

    private sealed record ReceiptBody(string? Number);

    private (WebApplicationFactory<Program> App, CapturingEmailProvider Email) Host(int limit = 1000)
    {
        var portal = PortalApp.Create(factory, new Dictionary<string, string?> { ["WebForms:RateLimitRequests"] = limit.ToString() });
        var app = portal.App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICaptchaVerifier>();
            services.AddSingleton<ICaptchaVerifier>(new FakeCaptcha());
        }));
        return (app, portal.Email);
    }

    private static object Body(string email, string? subject = "Printer broken", string captcha = "good") =>
        new { name = "Nour Ali", email, subject, message = "It prints blank pages.", captchaToken = captcha };

    private static string NewEmail() => $"visitor-{Guid.NewGuid():N}@forms.example";

    [Fact]
    public async Task Post_CreatesATicket_WithChannelWebForm()
    {
        var (app, _) = Host();

        var response = await app.CreateClient().PostAsJsonAsync(Path, Body(NewEmail()));
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptBody>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Matches("^TKT-[0-9]{6}$", receipt!.Number);
        using var scope = app.Services.CreateScope();
        var number = int.Parse(receipt.Number![4..], System.Globalization.CultureInfo.InvariantCulture);
        var ticket = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.AsNoTracking().SingleAsync(t => t.Number == number);
        Assert.Equal((TicketChannel.WebForm, "Printer broken"), (ticket.Channel, ticket.Subject));
    }

    [Fact]
    public async Task Post_BeyondTheLimit_Returns429_WithRetryAfter()
    {
        var (app, _) = Host(limit: 2);
        var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Path, Body(NewEmail()))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Path, Body(NewEmail()))).StatusCode);
        var third = await client.PostAsJsonAsync(Path, Body(NewEmail()));

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("application/problem+json", third.Content.Headers.ContentType?.MediaType);
        Assert.True(third.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Post_WithAFailedCaptcha_Returns400_OnCaptchaToken()
    {
        var (app, _) = Host();

        var response = await app.CreateClient().PostAsJsonAsync(Path, Body(NewEmail(), captcha: "bad"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("captchaToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_MissingFields_Returns400_WithFieldErrors()
    {
        var (app, _) = Host();

        var response = await app.CreateClient().PostAsJsonAsync(Path, new { name = "", email = "", subject = "", message = "", captchaToken = "good" });
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.All(new[] { "name", "email", "subject", "message" }, field => Assert.Contains($"\"{field}\"", text));
    }

    [Fact]
    public async Task Post_SendsAConfirmationEmail_WithTheTicketNumber()
    {
        var (app, mail) = Host();
        var email = NewEmail();

        var receipt = await (await app.CreateClient().PostAsJsonAsync(Path, Body(email))).Content.ReadFromJsonAsync<ReceiptBody>();

        var sent = Assert.Single(mail.Sent, m => m.Recipient == email);
        Assert.Contains(receipt!.Number!, sent.Body);
        Assert.Contains("[TKT-", sent.Subject);
    }

    [Fact]
    public async Task Config_IsAnonymous_AndTellsWhetherACaptchaIsUsed()
    {
        var (app, _) = Host();

        var response = await app.CreateClient().GetAsync($"{Path}/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("captchaRequired", await response.Content.ReadAsStringAsync());
    }
}
