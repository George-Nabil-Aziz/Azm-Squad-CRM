using System.Net.Http.Headers;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Crm.Api.IntegrationTests.Ai;

/// <summary>A test host whose AI provider is a <see cref="FakeAiTextService"/>, with signed-in staff clients.</summary>
internal sealed class AiApp
{
    private readonly CrmApiFactory _factory;

    public AiApp(CrmApiFactory factory, FakeAiTextService? fake = null, Action<Microsoft.AspNetCore.Hosting.IWebHostBuilder>? configure = null)
    {
        _factory = factory;
        Fake = fake ?? new FakeAiTextService();
        App = configure is null
            ? FakeAiTextService.Host(factory, Fake)
            : FakeAiTextService.Host(factory, Fake).WithWebHostBuilder(configure);
    }

    public FakeAiTextService Fake { get; }

    public WebApplicationFactory<Program> App { get; }

    public HttpClient Anonymous() => App.CreateClient();

    public HttpClient Authenticated(string token)
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A client signed in as a new user with the role (SuperAdmin: the seeded admin).</summary>
    public async Task<HttpClient> StaffAsync(string role)
    {
        if (role == "SuperAdmin")
        {
            return Authenticated(await _factory.LoginAsync());
        }

        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@crm.local";
        await _factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, role);
        return Authenticated(await _factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
    }
}
