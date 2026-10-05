using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The one shared test host (CLAUDE.md "Integration tests"). Environment <c>Testing</c>.
/// Captures logs in <see cref="Logs"/> and maps test-only endpoints under <c>/_test</c>.
/// Later stories add SQLite in-memory, JWT settings, etc. here.
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>
{
    public TestLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, TestEndpointsStartupFilter>();
            services.AddScoped<FluentValidation.IValidator<SampleRequest>, SampleRequestValidator>();
        });
    }
}
