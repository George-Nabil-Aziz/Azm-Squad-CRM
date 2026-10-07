using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Domain.Tasks;
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

/// <summary>The development demo tasks step: exactly 10 per demo staff account, idempotent, top-up only, behind the same switches.</summary>
public class DemoTaskSeederTests
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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DemoDataSeeder.FlagKey] = flag?.ToString() })
            .Build();
        var services = scope.ServiceProvider;
        return await new DemoTaskSeeder(
            services.GetRequiredService<CrmDbContext>(),
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<TimeProvider>(),
            config,
            new FakeEnvironment(environment),
            NullLogger<DemoTaskSeeder>.Instance).SeedAsync(CancellationToken.None);
    }

    private static async Task<T> QueryAsync<T>(CrmApiFactory factory, Func<CrmDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CrmDbContext>());
    }

    private static async Task<Dictionary<string, Guid>> CreateDemoAccountsAsync(CrmApiFactory factory)
    {
        var ids = new Dictionary<string, Guid>();
        foreach (var email in DemoTaskSeeder.AccountEmails)
        {
            using var scope = factory.Services.CreateScope();
            var existing = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
            ids[email] = existing?.Id ?? await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, "Agent");
        }

        return ids;
    }

    [Fact]
    public async Task EveryDemoAccount_EndsUpWithExactlyTenTasks_WithTheExpectedMix()
    {
        using var factory = new CrmApiFactory();
        var ids = await CreateDemoAccountsAsync(factory);
        var now = factory.Time.GetUtcNow().UtcDateTime;

        Assert.Equal(40, await SeedAsync(factory));

        foreach (var id in ids.Values)
        {
            var tasks = await QueryAsync(factory, db => db.Tasks.AsNoTracking().Where(t => t.OwnerId == id).ToListAsync());
            Assert.Equal(10, tasks.Count);
            Assert.Equal(2, tasks.Count(t => t.IsDone));
            Assert.Equal(2, tasks.Count(t => !t.IsDone && t.DueAt < now));
            Assert.Equal(3, tasks.Count(t => !t.IsDone && t.DueAt >= now && t.DueAt < now.AddHours(12)));
            Assert.Equal(3, tasks.Count(t => !t.IsDone && t.DueAt >= now.AddHours(24)));
            Assert.All(tasks, t => Assert.True(t.DueAt > t.CreatedAt && t.CreatedAt <= now));
            Assert.Contains(tasks, t => t.Title.Any(ch => ch is >= '؀' and <= 'ۿ'));
            Assert.Contains(tasks, t => t.Title.All(ch => ch < '؀'));
        }

        Assert.Equal(0, await QueryAsync(factory, db => db.Notifications.CountAsync()));
    }

    [Fact]
    public async Task RunningTwice_AddsNothing()
    {
        using var factory = new CrmApiFactory();
        await CreateDemoAccountsAsync(factory);
        await SeedAsync(factory);

        Assert.Equal(0, await SeedAsync(factory));

        Assert.Equal(40, await QueryAsync(factory, db => db.Tasks.CountAsync()));
    }

    [Fact]
    public async Task AUserWithThreeTasks_IsToppedUpToTen_AndExistingTasksAreKept()
    {
        using var factory = new CrmApiFactory();
        var ids = await CreateDemoAccountsAsync(factory);
        var owner = ids["agent@crm.com"];
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var original = new List<Guid>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            for (var i = 0; i < 3; i++)
            {
                var task = WorkTask.Create(owner, $"mine {i}", null, now.AddDays(i + 1), null, now);
                original.Add(task.Id);
                db.Tasks.Add(task);
            }

            await db.SaveChangesAsync();
        }

        await SeedAsync(factory);

        Assert.Equal(10, await QueryAsync(factory, db => db.Tasks.CountAsync(t => t.OwnerId == owner)));
        Assert.Equal(3, await QueryAsync(factory, db => db.Tasks.CountAsync(t => original.Contains(t.Id))));
    }

    [Fact]
    public async Task AUserWithMoreThanTenTasks_GetsNoneAdded_AndMissingAccountsAreSkipped()
    {
        using var factory = new CrmApiFactory();
        var owner = await factory.CreateUserAsync("agent@crm.com", CrmApiFactory.TestUserPassword, "Agent");
        var now = factory.Time.GetUtcNow().UtcDateTime;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            for (var i = 0; i < 12; i++)
            {
                db.Tasks.Add(WorkTask.Create(owner, $"mine {i}", null, now.AddDays(i + 1), null, now));
            }

            await db.SaveChangesAsync();
        }

        Assert.Equal(10, await SeedAsync(factory)); // only the seeded SuperAdmin exists besides the agent

        Assert.Equal(12, await QueryAsync(factory, db => db.Tasks.CountAsync(t => t.OwnerId == owner)));
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Development", null)]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    public async Task DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();
        await CreateDemoAccountsAsync(factory);

        Assert.Equal(0, await SeedAsync(factory, environment, flag));

        Assert.Equal(0, await QueryAsync(factory, db => db.Tasks.CountAsync()));
    }
}
