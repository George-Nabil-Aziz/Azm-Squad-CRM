using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Audit;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Domain.Audit;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.DemoData;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crm.Api.IntegrationTests.Persistence;

/// <summary>Demo audit-log entries so the audit page has varied examples; only when the log is nearly empty.</summary>
public class DemoAuditSeederTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Crm.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<int> SeedAsync(CrmApiFactory factory, string environment = "Development", bool? flag = true)
    {
        using var scope = factory.Services.CreateScope();
        var s = scope.ServiceProvider;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DemoDataSeeder.FlagKey] = flag?.ToString() })
            .Build();
        return await new DemoAuditSeeder(
            s.GetRequiredService<CrmDbContext>(), s.GetRequiredService<UserManager<ApplicationUser>>(), s.GetRequiredService<TimeProvider>(),
            config, new FakeEnvironment(environment), NullLogger<DemoAuditSeeder>.Instance).SeedAsync(CancellationToken.None);
    }

    private static async Task<List<AuditLogEntry>> EntriesAsync(CrmApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().AuditLog.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Seeds_VariedEntries_OfEveryKindTheAuditPageDescribes_WithFriendlyIps()
    {
        using var factory = new CrmApiFactory();
        var before = (await EntriesAsync(factory)).Count;

        var added = await SeedAsync(factory);

        Assert.True(added >= 12);
        var entries = await EntriesAsync(factory);
        Assert.Equal(before + added, entries.Count);
        foreach (var action in new[]
                 {
                     AuditActions.LoginSucceeded, AuditActions.LoginFailed, AuditActions.UserCreated, AuditActions.UserUpdated,
                     AuditActions.UserDeactivated, AuditActions.SlaPolicyUpdated, AuditActions.CustomerDeleted, AuditActions.SettingsUpdated,
                     AuditActions.BrandingUpdated,
                 })
        {
            Assert.Contains(entries, e => e.Action == action);
        }

        Assert.Contains(entries, e => e.IpAddress == "::1");
        Assert.Contains(entries, e => e.IpAddress is { } ip && ip.StartsWith("203.0.113.", StringComparison.Ordinal));
        Assert.All(entries, e => Assert.Equal(DateTimeKind.Utc, e.OccurredAt.Kind));
    }

    [Fact]
    public async Task RunningTwice_AddsNothing()
    {
        using var factory = new CrmApiFactory();
        await SeedAsync(factory);
        var count = (await EntriesAsync(factory)).Count;

        Assert.Equal(0, await SeedAsync(factory));

        Assert.Equal(count, (await EntriesAsync(factory)).Count);
    }

    [Fact]
    public async Task ThePageEndpoint_ReturnsTheSeededEntries()
    {
        using var factory = new CrmApiFactory();
        await SeedAsync(factory);
        using var admin = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var page = await admin.GetFromJsonAsync<AuditLogPageBody>("/api/audit-logs?pageSize=100");

        Assert.True(page!.TotalCount >= 12);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Development", null)]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    public async Task DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();
        var before = (await EntriesAsync(factory)).Count;

        Assert.Equal(0, await SeedAsync(factory, environment, flag));

        Assert.Equal(before, (await EntriesAsync(factory)).Count);
    }
}
