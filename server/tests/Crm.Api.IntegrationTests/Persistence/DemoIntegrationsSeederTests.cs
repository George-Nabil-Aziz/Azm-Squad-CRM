using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Settings;
using Crm.Domain.Customers;
using Crm.Domain.Integrations;
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

/// <summary>Demo API keys, webhooks with a delivery log and ERP links with sync logs (CRM-58..60): counts, idempotency, switches, no usable secrets, nothing queued for sending.</summary>
public class DemoIntegrationsSeederTests
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
        return await new DemoIntegrationsSeeder(
            s.GetRequiredService<CrmDbContext>(), s.GetRequiredService<UserManager<ApplicationUser>>(), s.GetRequiredService<ISecretProtector>(),
            s.GetRequiredService<TimeProvider>(), config, new FakeEnvironment(environment), NullLogger<DemoIntegrationsSeeder>.Instance)
            .SeedAsync(CancellationToken.None);
    }

    private static async Task<T> QueryAsync<T>(CrmApiFactory factory, Func<CrmDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CrmDbContext>());
    }

    private static async Task AddDemoCustomersAsync(CrmApiFactory factory, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            db.Customers.Add(Customer.Create($"Demo Customer {i}", $"c{i}@demo.crm.com", null, now.AddDays(-i - 1)));
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Seeds_ApiKeys_WithScopes_LastUsedAndOneRevoked_AndNoUsablePlainKey()
    {
        using var factory = new CrmApiFactory();

        Assert.True(await SeedAsync(factory) > 0);

        var keys = await QueryAsync(factory, db => db.ApiKeys.AsNoTracking().ToListAsync());
        Assert.Equal(3, keys.Count);
        Assert.Contains(keys, k => k.Name == "Website" && !k.IsRevoked && k.LastUsedAt is not null && k.HasScope(ApiKeyScopes.TicketsWrite));
        Assert.Contains(keys, k => k.Name == "Mobile app" && !k.IsRevoked && k.LastUsedAt is not null);
        Assert.Single(keys, k => k.IsRevoked);
        Assert.All(keys, k => Assert.Equal(64, k.KeyHash.Length));
        Assert.All(keys, k => Assert.StartsWith(ApiKey.Prefix, k.KeyPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seeds_TwoWebhooks_OneActiveOneDisabled_WithAnEncryptedSecret()
    {
        using var factory = new CrmApiFactory();

        await SeedAsync(factory);

        var hooks = await QueryAsync(factory, db => db.Webhooks.AsNoTracking().ToListAsync());
        Assert.Equal(2, hooks.Count);
        var active = Assert.Single(hooks, h => h.IsEnabled);
        Assert.Equal("https://example.com/hooks/crm", active.Url);
        Assert.Single(hooks, h => !h.IsEnabled);
        Assert.All(hooks, h => Assert.DoesNotContain("whsec_", h.ProtectedSecret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seeds_TenDeliveries_MostlyDelivered_SomeFailedWithRetries_NoneLeftToSend()
    {
        using var factory = new CrmApiFactory();

        await SeedAsync(factory);

        var deliveries = await QueryAsync(factory, db => db.WebhookDeliveries.AsNoTracking().ToListAsync());
        Assert.Equal(10, deliveries.Count);
        Assert.True(deliveries.Count(d => d.Status == WebhookDeliveryStatus.Delivered) >= 6);
        Assert.True(deliveries.Count(d => d.Status == WebhookDeliveryStatus.Failed) >= 2);
        Assert.Contains(deliveries, d => d.Status == WebhookDeliveryStatus.Delivered && d.Attempts > 1);
        Assert.DoesNotContain(deliveries, d => d.Status == WebhookDeliveryStatus.Pending); // the delivery job would send them
        Assert.All(deliveries, d => Assert.StartsWith("{", d.Payload, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Links_TenDemoCustomersToTheErp_AndWritesSyncLogs()
    {
        using var factory = new CrmApiFactory();
        await AddDemoCustomersAsync(factory, 12);

        await SeedAsync(factory);

        var linked = await QueryAsync(factory, db => db.Customers.Where(c => c.ErpCustomerId != null).Select(c => c.ErpCustomerId!).ToListAsync());
        Assert.Equal(10, linked.Count);
        var logs = await QueryAsync(factory, db => db.ErpSyncLogs.AsNoTracking().ToListAsync());
        Assert.InRange(logs.Count, 6, 14);
        Assert.Contains(logs, l => l.Result == ErpSyncResult.Success);
        Assert.Contains(logs, l => l.Result == ErpSyncResult.Failed && !string.IsNullOrEmpty(l.Error));
        Assert.All(logs, l => Assert.Contains(l.ErpCustomerId, linked));
    }

    [Fact]
    public async Task RunningTwice_AddsNothing()
    {
        using var factory = new CrmApiFactory();
        await AddDemoCustomersAsync(factory, 12);
        await SeedAsync(factory);
        var before = await QueryAsync(factory, Counts);

        Assert.Equal(0, await SeedAsync(factory));

        Assert.Equal(before, await QueryAsync(factory, Counts));
    }

    private static async Task<(int, int, int, int)> Counts(CrmDbContext db) =>
        (await db.ApiKeys.CountAsync(), await db.Webhooks.CountAsync(), await db.WebhookDeliveries.CountAsync(), await db.ErpSyncLogs.CountAsync());

    [Fact]
    public async Task ShowsUpInTheApi_ForAnAdministrator()
    {
        using var factory = new CrmApiFactory();
        await SeedAsync(factory);
        using var admin = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var keys = await admin.GetStringAsync("/api/api-keys");
        var hooks = await admin.GetStringAsync("/api/webhooks");

        Assert.Contains("Website", keys, StringComparison.Ordinal);
        Assert.Contains("https://example.com/hooks/crm", hooks, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Development", null)]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    public async Task DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();
        await AddDemoCustomersAsync(factory, 12);

        Assert.Equal(0, await SeedAsync(factory, environment, flag));

        Assert.Equal((0, 0, 0, 0), await QueryAsync(factory, Counts));
        Assert.Equal(0, await QueryAsync(factory, db => db.Customers.CountAsync(c => c.ErpCustomerId != null)));
    }
}
