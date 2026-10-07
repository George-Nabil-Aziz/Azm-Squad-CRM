using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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

/// <summary>Arrange helpers shared by the AI integration tests.</summary>
internal static class AiArrange
{
    private sealed record IdBody(Guid Id);

    /// <summary>A knowledge base article (published by default) created by <paramref name="admin"/> in a new category.</summary>
    public static async Task<Guid> ArticleAsync(
        HttpClient admin, string title, string body, bool publish = true, string? titleAr = null, string? bodyAr = null)
    {
        var category = await (await admin.PostAsJsonAsync("/api/kb/categories", new { nameEn = $"Cat {Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<IdBody>();
        var created = await admin.PostAsJsonAsync("/api/kb/articles",
            new { categoryId = category!.Id, titleEn = title, bodyEn = body, titleAr, bodyAr });
        Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);
        var article = await created.Content.ReadFromJsonAsync<IdBody>();
        if (publish)
        {
            Assert.True((await admin.PostAsync($"/api/kb/articles/{article!.Id}/publish", null)).IsSuccessStatusCode);
        }

        return article!.Id;
    }

    /// <summary>A message from the customer (channels create these; the API has no endpoint), written straight to the database.</summary>
    public static async Task AddCustomerMessageAsync(AiApp app, CrmApiFactory factory, Guid ticketId, string body)
    {
        using var scope = app.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Crm.Infrastructure.Persistence.CrmDbContext>();
        db.TicketMessages.Add(Crm.Domain.Tickets.TicketMessage.Inbound(
            ticketId, body, Crm.Domain.Tickets.TicketChannel.Email, null, factory.Time.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();
    }
}
