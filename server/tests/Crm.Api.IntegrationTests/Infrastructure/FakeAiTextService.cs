using Crm.Application.Ai;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The AI provider of the test hosts: answers with <see cref="Answer"/> (or the next of <see cref="Answers"/>), can be
/// switched to "not configured" or to failing, and remembers every request it got (no network).
/// </summary>
public sealed class FakeAiTextService : IAiTextService
{
    private readonly object _lock = new();

    public bool IsConfigured { get; set; } = true;

    public string Answer { get; set; } = "- Fake summary";

    /// <summary>Optional answers by call number; the last one repeats.</summary>
    public List<string> Answers { get; } = [];

    public bool Fail { get; set; }

    private readonly List<AiRequest> _requests = [];

    public IReadOnlyList<AiRequest> Requests
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    public Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        int call;
        lock (_lock)
        {
            _requests.Add(request);
            call = _requests.Count;
        }

        if (Fail)
        {
            throw new AiFailedException("The fake AI failed.");
        }

        return Task.FromResult(Answers.Count == 0 ? Answer : Answers[Math.Min(call, Answers.Count) - 1]);
    }

    /// <summary>A host of <paramref name="factory"/> whose AI provider is <paramref name="fake"/>.</summary>
    public static WebApplicationFactory<Program> Host(CrmApiFactory factory, FakeAiTextService fake) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiTextService>();
            services.AddSingleton<IAiTextService>(fake);
        }));
}
