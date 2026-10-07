using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.QuickReplies;
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

/// <summary>The development demo quick replies: shared + personal, bilingual with placeholders, idempotent, behind the switches.</summary>
public class DemoQuickReplySeederTests
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
        return await new DemoQuickReplySeeder(
            s.GetRequiredService<CrmDbContext>(), s.GetRequiredService<UserManager<ApplicationUser>>(), s.GetRequiredService<TimeProvider>(),
            config, new FakeEnvironment(environment), NullLogger<DemoQuickReplySeeder>.Instance).SeedAsync(CancellationToken.None);
    }

    private static async Task<T> QueryAsync<T>(CrmApiFactory factory, Func<CrmDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CrmDbContext>());
    }

    [Fact]
    public async Task Seeds_EightSharedAndThreePersonalReplies_WithPlaceholdersInBothLanguages()
    {
        using var factory = new CrmApiFactory();
        var agent = await factory.CreateUserAsync("agent@crm.com", CrmApiFactory.TestUserPassword, "Agent");

        Assert.Equal(11, await SeedAsync(factory));

        var all = await QueryAsync(factory, db => db.QuickReplies.AsNoTracking().ToListAsync());
        Assert.Equal(8, all.Count(r => r.IsShared));
        Assert.Equal(3, all.Count(r => !r.IsShared && r.OwnerId == agent));
        Assert.All(all, r => Assert.Contains(r.Body, b => b is >= '؀' and <= 'ۿ'));
        Assert.All(all, r => Assert.Contains(r.Body, b => b is >= 'a' and <= 'z'));
        var text = string.Join(' ', all.Select(r => r.Body));
        foreach (var placeholder in new[] { "{{customer.name}}", "{{ticket.number}}", "{{ticket.subject}}", "{{agent.name}}" })
        {
            Assert.Contains(placeholder, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RunningTwice_AddsNothing_AndTheListShowsSharedPlusOwnReplies()
    {
        using var factory = new CrmApiFactory();
        await factory.CreateUserAsync("agent@crm.com", CrmApiFactory.TestUserPassword, "Agent");
        await SeedAsync(factory);

        Assert.Equal(0, await SeedAsync(factory));

        Assert.Equal(11, await QueryAsync(factory, db => db.QuickReplies.CountAsync()));
        using var mine = factory.CreateAuthenticatedClient(await factory.LoginAsync("agent@crm.com", CrmApiFactory.TestUserPassword));
        Assert.Equal(11, (await mine.GetFromJsonAsync<List<QuickReplyResponse>>("/api/quick-replies"))!.Count);
        using var other = await factory.CreateClientWithRoleAsync("Agent");
        Assert.Equal(8, (await other.GetFromJsonAsync<List<QuickReplyResponse>>("/api/quick-replies"))!.Count);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Development", null)]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    public async Task DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();
        await factory.CreateUserAsync("agent@crm.com", CrmApiFactory.TestUserPassword, "Agent");

        Assert.Equal(0, await SeedAsync(factory, environment, flag));

        Assert.Equal(0, await QueryAsync(factory, db => db.QuickReplies.CountAsync()));
    }
}
