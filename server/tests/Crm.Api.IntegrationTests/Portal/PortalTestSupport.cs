using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Channels;
using Crm.Domain.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Portal;

/// <summary>An email provider that records what the portal sends (no network).</summary>
internal sealed class CapturingEmailProvider : IChannelProvider
{
    public ChannelKind Channel => ChannelKind.Email;

    public bool IsConfigured => true;

    public List<OutboundChannelMessage> Sent { get; } = [];

    public Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
    {
        lock (Sent)
        {
            Sent.Add(message);
        }

        return Task.FromResult(ChannelSendResult.Ok($"provider-{Guid.NewGuid():N}"));
    }

    /// <summary>The 6-digit code of the last mail to the address.</summary>
    public string LastCodeFor(string email)
    {
        OutboundChannelMessage mail;
        lock (Sent)
        {
            mail = Sent.Last(m => m.Recipient == email);
        }

        return Regex.Match(mail.Body, @"\b\d{6}\b").Value;
    }

    public int CountTo(string email)
    {
        lock (Sent)
        {
            return Sent.Count(m => m.Recipient == email);
        }
    }
}

internal sealed record PortalLoginBody(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, PortalCustomerBody Customer);

internal sealed record PortalCustomerBody(Guid Id, string Name, string Email);

/// <summary>A test host whose email channel records messages, plus helpers to sign portal customers in.</summary>
internal sealed class PortalApp
{
    private PortalApp(CrmApiFactory factory, WebApplicationFactory<Program> app, CapturingEmailProvider email)
    {
        Factory = factory;
        App = app;
        Email = email;
    }

    public CrmApiFactory Factory { get; }

    public WebApplicationFactory<Program> App { get; }

    public CapturingEmailProvider Email { get; }

    public static PortalApp Create(CrmApiFactory factory, IDictionary<string, string?>? settings = null)
    {
        var email = new CapturingEmailProvider();
        var app = factory.WithWebHostBuilder(builder =>
        {
            if (settings is not null)
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChannelProvider>();
                services.AddSingleton<IChannelProvider>(email);
            });
        });
        return new PortalApp(factory, app, email);
    }

    public static string NewEmail() => $"customer-{Guid.NewGuid():N}@portal.example";

    public HttpClient Anonymous() => App.CreateClient();

    public HttpClient Authenticated(string token)
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A staff client (SuperAdmin) on this host.</summary>
    public async Task<HttpClient> StaffAsync() => Authenticated(await Factory.LoginAsync());

    public async Task RequestCodeAsync(string email)
    {
        var response = await Anonymous().PostAsJsonAsync("/api/portal/auth/request-code", new { email });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    public Task<HttpResponseMessage> VerifyAsync(string email, string code) =>
        Anonymous().PostAsJsonAsync("/api/portal/auth/verify", new { email, code });

    /// <summary>Requests a code, reads it from the mail and signs in; returns the token and the customer.</summary>
    public async Task<(string Token, PortalCustomerBody Customer)> SignInAsync(string? email = null)
    {
        email ??= NewEmail();
        await RequestCodeAsync(email);
        var response = await VerifyAsync(email, Email.LastCodeFor(email));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PortalLoginBody>())!;
        return (body.AccessToken, body.Customer);
    }
}
