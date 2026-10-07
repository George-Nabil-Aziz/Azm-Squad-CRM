using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Chat;
using Crm.Domain.Chat;
using Crm.Domain.Tickets;
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

/// <summary>The development demo live chats: counts, transcripts as Chat tickets, the staff list, idempotency and the switches.</summary>
public class DemoChatSeederTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Crm.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(bool? flag) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:SuperAdminPassword"] = CrmApiFactory.SuperAdminPassword,
            [DemoDataSeeder.FlagKey] = flag?.ToString(),
        })
        .Build();

    /// <summary>Seeds the main demo data first (the chats use its customers), then runs the chat step.</summary>
    private static async Task<int> SeedChatsAsync(CrmApiFactory factory, string environment = "Development", bool? flag = true, bool mainFirst = true)
    {
        using var scope = factory.Services.CreateScope();
        var s = scope.ServiceProvider;
        if (mainFirst)
        {
            await new DemoDataSeeder(
                s.GetRequiredService<CrmDbContext>(), s.GetRequiredService<UserManager<ApplicationUser>>(), s.GetRequiredService<TimeProvider>(),
                Config(true), new FakeEnvironment("Development"), NullLogger<DemoDataSeeder>.Instance).SeedAsync(CancellationToken.None);
        }

        return await new DemoChatSeeder(
            s.GetRequiredService<CrmDbContext>(), s.GetRequiredService<UserManager<ApplicationUser>>(), s.GetRequiredService<TimeProvider>(),
            Config(flag), new FakeEnvironment(environment), NullLogger<DemoChatSeeder>.Instance).SeedAsync(CancellationToken.None);
    }

    private static async Task<T> QueryAsync<T>(CrmApiFactory factory, Func<CrmDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CrmDbContext>());
    }

    private static async Task<int> TopUpAsync(CrmApiFactory factory, string environment = "Development", bool? flag = true)
    {
        using var scope = factory.Services.CreateScope();
        var s = scope.ServiceProvider;
        return await new DemoChatSeeder(
            s.GetRequiredService<CrmDbContext>(), s.GetRequiredService<UserManager<ApplicationUser>>(), s.GetRequiredService<TimeProvider>(),
            Config(flag), new FakeEnvironment(environment), NullLogger<DemoChatSeeder>.Instance).TopUpAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TopUp_AddsTwoChats_ToFifteen_AndTwoOfflineFormChatTickets()
    {
        using var factory = new CrmApiFactory();
        await SeedChatsAsync(factory);
        var before = await QueryAsync(factory, db => db.Tickets.Where(t => t.Channel == TicketChannel.Chat).Select(t => t.Id).ToListAsync());

        Assert.Equal(4, await TopUpAsync(factory));

        var sessions = await QueryAsync(factory, db => db.ChatSessions.AsNoTracking().Include(s => s.Messages).ToListAsync());
        Assert.Equal(15, sessions.Count);
        Assert.Equal(10, sessions.Count(s => s.Status == ChatStatus.Ended));
        Assert.Equal(3, sessions.Count(s => s.Status == ChatStatus.Waiting));
        Assert.Equal(2, sessions.Count(s => s.Status == ChatStatus.Active));
        Assert.Equal(2, sessions.Where(s => s.Status == ChatStatus.Active).Select(s => s.AgentId).Distinct().Count());
        Assert.Contains(sessions, s => s.Status == ChatStatus.Waiting && s.Messages.Any(m => m.Body.Any(c => c is >= '؀' and <= 'ۿ')));
        var chatTickets = await QueryAsync(factory, db => db.Tickets.AsNoTracking().Where(t => t.Channel == TicketChannel.Chat).ToListAsync());
        var offlineOnly = chatTickets.Where(t => !before.Contains(t.Id)).ToList();
        Assert.Equal(2, offlineOnly.Count);
        Assert.Contains(offlineOnly, t => t.Subject.Any(c => c is >= '؀' and <= 'ۿ'));
        Assert.Contains(offlineOnly, t => t.Subject.All(c => c < '؀'));
        var numbers = await QueryAsync(factory, db => db.Tickets.Select(t => t.Number).ToListAsync());
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public async Task TopUp_RunningTwice_AddsNothing()
    {
        using var factory = new CrmApiFactory();
        await SeedChatsAsync(factory);
        await TopUpAsync(factory);

        Assert.Equal(0, await TopUpAsync(factory));

        Assert.Equal(15, await QueryAsync(factory, db => db.ChatSessions.CountAsync()));
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task TopUp_DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();
        await SeedChatsAsync(factory);

        Assert.Equal(0, await TopUpAsync(factory, environment, flag));

        Assert.Equal(13, await QueryAsync(factory, db => db.ChatSessions.CountAsync()));
    }

    [Fact]
    public async Task Seeds_TenEndedChats_WithAChatTicketAndTheTranscript_AndThreeOpenChats()
    {
        using var factory = new CrmApiFactory();
        var now = factory.Time.GetUtcNow().UtcDateTime;

        Assert.Equal(13, await SeedChatsAsync(factory));

        var sessions = await QueryAsync(factory, db => db.ChatSessions.AsNoTracking().Include(s => s.Messages).ToListAsync());
        Assert.Equal(13, sessions.Count);
        Assert.All(sessions, s => Assert.EndsWith("@demo.crm.com", s.VisitorEmail));
        var ended = sessions.Where(s => s.Status == ChatStatus.Ended).ToList();
        Assert.Equal(10, ended.Count);
        Assert.Equal(2, sessions.Count(s => s.Status == ChatStatus.Waiting));
        Assert.Equal(1, sessions.Count(s => s.Status == ChatStatus.Active));
        Assert.All(ended, s =>
        {
            Assert.InRange(s.Messages.Count, 4, 10);
            Assert.NotNull(s.AgentId);
            Assert.True(s.EndedAt <= now && s.StartedAt >= now.AddDays(-15));
            Assert.NotNull(s.TicketId);
        });
        Assert.Contains(ended, s => s.AgentName != null && s.AgentId == ended.First().AgentId);
        Assert.Contains(sessions, s => s.Messages.Any(m => m.Body.Any(c => c is >= '؀' and <= 'ۿ')));
        Assert.Contains(sessions, s => s.Messages.All(m => m.Body.All(c => c < '؀')));

        foreach (var session in ended)
        {
            var ticket = await QueryAsync(factory, db => db.Tickets.AsNoTracking().SingleAsync(t => t.Id == session.TicketId));
            Assert.Equal(TicketChannel.Chat, ticket.Channel);
            Assert.Equal(session.TicketNumber, ticket.DisplayNumber);
            Assert.Equal(ChatTranscript.Build(session.Messages), ticket.Description);
            Assert.NotNull(ticket.ResponseDueAt);
        }

        var numbers = await QueryAsync(factory, db => db.Tickets.Select(t => t.Number).ToListAsync());
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public async Task RunningTwice_AddsNothing()
    {
        using var factory = new CrmApiFactory();
        await SeedChatsAsync(factory);
        var tickets = await QueryAsync(factory, db => db.Tickets.CountAsync());

        Assert.Equal(0, await SeedChatsAsync(factory, mainFirst: false));

        Assert.Equal(13, await QueryAsync(factory, db => db.ChatSessions.CountAsync()));
        Assert.Equal(tickets, await QueryAsync(factory, db => db.Tickets.CountAsync()));
    }

    [Fact]
    public async Task TheStaffConsole_ListsTheOpenChats_ForTheDemoAgent()
    {
        using var factory = new CrmApiFactory();
        await factory.CreateUserAsync("agent@crm.com", CrmApiFactory.TestUserPassword, "Agent");
        await SeedChatsAsync(factory);
        using var client = factory.CreateAuthenticatedClient(await factory.LoginAsync("agent@crm.com", CrmApiFactory.TestUserPassword));

        var waiting = await client.GetFromJsonAsync<List<ChatSessionResponse>>("/api/chat-sessions?status=waiting");
        var active = await client.GetFromJsonAsync<List<ChatSessionResponse>>("/api/chat-sessions?status=active");

        Assert.Equal(2, waiting!.Count);
        var mine = Assert.Single(active!);
        Assert.Equal("active", mine.Status);
        var messages = await client.GetFromJsonAsync<List<ChatMessageResponse>>($"/api/chat-sessions/{mine.Id}/messages");
        Assert.True(messages!.Count >= 3);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Development", null)]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    public async Task DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();

        Assert.Equal(0, await SeedChatsAsync(factory, environment, flag, mainFirst: false));

        Assert.Equal(0, await QueryAsync(factory, db => db.ChatSessions.CountAsync()));
    }
}
