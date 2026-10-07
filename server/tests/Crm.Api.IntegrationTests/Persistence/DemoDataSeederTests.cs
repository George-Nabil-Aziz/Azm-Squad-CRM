using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Reports;
using Crm.Domain.Customers;
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

/// <summary>The Development demo data seeder: ranges of what it creates, idempotency, the two switches, and the reports it feeds.</summary>
public class DemoDataSeederTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Crm.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DemoDataSeeder NewSeeder(IServiceScope scope, string environment, bool? flag)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:SuperAdminPassword"] = CrmApiFactory.SuperAdminPassword,
                [DemoDataSeeder.FlagKey] = flag?.ToString(),
            })
            .Build();
        var services = scope.ServiceProvider;
        return new DemoDataSeeder(
            services.GetRequiredService<CrmDbContext>(),
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<TimeProvider>(),
            config,
            new FakeEnvironment(environment),
            NullLogger<DemoDataSeeder>.Instance);
    }

    private static async Task<bool> SeedAsync(CrmApiFactory factory, string environment = "Development", bool? flag = true)
    {
        using var scope = factory.Services.CreateScope();
        return await NewSeeder(scope, environment, flag).SeedAsync(CancellationToken.None);
    }

    private static async Task<T> QueryAsync<T>(CrmApiFactory factory, Func<CrmDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CrmDbContext>());
    }

    [Fact]
    public async Task Seed_CreatesRealisticAmounts()
    {
        using var factory = new CrmApiFactory();
        var now = factory.Time.GetUtcNow().UtcDateTime;

        Assert.True(await SeedAsync(factory));

        var customers = await QueryAsync(factory, db => db.Customers.CountAsync(c => c.Email!.EndsWith("@demo.crm.com")));
        var tickets = await QueryAsync(factory, db => db.Tickets.AsNoTracking().ToListAsync());
        Assert.InRange(customers, 55, 65);
        Assert.InRange(tickets.Count, 280, 320);
        Assert.Equal(tickets.Count, tickets.Select(t => t.Number).Distinct().Count());
        Assert.All(tickets, t => Assert.True(t.CreatedAt <= now && t.UpdatedAt <= now && t.CreatedAt >= now.AddDays(-61)));

        // Statuses: mostly finished, some still open; every channel is used.
        var finished = tickets.Count(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed);
        Assert.InRange(finished, tickets.Count * 55 / 100, tickets.Count * 95 / 100);
        Assert.InRange(tickets.Count(t => t.Status is TicketStatus.New or TicketStatus.Open or TicketStatus.Pending), 20, 140);
        Assert.Equal(7, tickets.Select(t => t.Channel).Distinct().Count());
        Assert.Equal(3, tickets.Select(t => t.Priority).Distinct().Count());
        Assert.True(tickets.Select(t => t.CategoryId).Distinct().Count() >= 5);
        Assert.All(tickets, t => Assert.NotNull(t.DepartmentId));
        Assert.All(tickets, t => Assert.NotNull(t.BranchId));

        // Resolved tickets have a resolution time, a first response and SLA due times; agents are loaded unevenly.
        Assert.All(tickets.Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed), t =>
        {
            Assert.NotNull(t.ResolvedAt);
            Assert.NotNull(t.FirstResponseAt);
            Assert.True(t.ResolvedAt >= t.FirstResponseAt && t.FirstResponseAt >= t.CreatedAt);
        });
        Assert.All(tickets, t => Assert.True(t.ResponseDueAt > t.CreatedAt && t.ResolutionDueAt > t.ResponseDueAt));
        var perAgent = tickets.Where(t => t.AssigneeId is not null).GroupBy(t => t.AssigneeId).Select(g => g.Count()).OrderBy(c => c).ToList();
        Assert.True(perAgent.Count >= 4);
        Assert.True(perAgent[^1] >= perAgent[0] * 2, "the workload should be uneven");

        // About 8-12 % breach the SLA (a wider band keeps the test stable at any time of day).
        var breached = tickets.Count(t => t.IsResponseBreachedAt(now) || t.IsResolutionBreachedAt(now));
        Assert.InRange(breached, tickets.Count * 5 / 100, tickets.Count * 16 / 100);

        // Weekdays are busier than weekends (Friday/Saturday), working hours busier than the night.
        double Average(IEnumerable<Ticket> list, int days) => list.Count() / (double)Math.Max(days, 1);
        var weekendDays = Enumerable.Range(0, 60).Count(d => now.Date.AddDays(-d).DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday);
        var weekend = tickets.Where(t => t.CreatedAt.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday);
        var weekdayList = tickets.Except(weekend);
        Assert.True(Average(weekdayList, 60 - weekendDays) > Average(weekend, weekendDays));
        Assert.True(tickets.Count(t => t.CreatedAt.Hour is >= 5 and <= 13) > tickets.Count / 2);

        // Conversations: public replies, internal notes and customer messages; nothing is queued for sending.
        var messages = await QueryAsync(factory, db => db.TicketMessages.AsNoTracking().ToListAsync());
        Assert.Contains(messages, m => m.Direction == MessageDirection.Outbound);
        Assert.Contains(messages, m => m.Direction == MessageDirection.InternalNote);
        Assert.Contains(messages, m => m.Direction == MessageDirection.Inbound);
        Assert.DoesNotContain(messages, m => m.DeliveryStatus == MessageDeliveryStatus.Pending);
        Assert.Equal(0, await QueryAsync(factory, db => db.OutboundMessages.CountAsync()));
        Assert.Equal(0, await QueryAsync(factory, db => db.ReceivedMessages.CountAsync()));

        // CSAT: about 60 % of the resolved tickets are rated, mostly 4-5.
        var ratings = await QueryAsync(factory, db => db.TicketSurveys.AsNoTracking().Where(s => s.Rating != null).Select(s => s.Rating!.Value).ToListAsync());
        Assert.InRange(ratings.Count, finished * 40 / 100, finished * 80 / 100);
        Assert.True(ratings.Count(r => r >= 4) > ratings.Count / 2);
        Assert.Contains(ratings, r => r <= 3);

        // Phone numbers are E.164, there are Arabic and English customers, two branches, departments, KB, tasks and notifications.
        var phones = await QueryAsync(factory, db => db.Customers.AsNoTracking().Select(c => c.Phone).Where(p => p != null).ToListAsync());
        Assert.All(phones, p => Assert.Matches(@"^\+[1-9]\d{7,14}$", p!));
        var names = await QueryAsync(factory, db => db.Customers.AsNoTracking().Where(c => c.Email!.EndsWith("@demo.crm.com")).Select(c => c.Name).ToListAsync());
        Assert.Contains(names, n => n.Any(ch => ch is >= '؀' and <= 'ۿ'));
        Assert.Contains(names, n => n.All(ch => ch < '؀'));
        Assert.True(await QueryAsync(factory, db => db.Branches.CountAsync()) >= 2);
        Assert.True(await QueryAsync(factory, db => db.Departments.CountAsync()) >= 3);
        Assert.InRange(await QueryAsync(factory, db => db.KbArticles.CountAsync(a => a.Status == Crm.Domain.KnowledgeBase.KbArticleStatus.Published)), 8, 12);
        Assert.InRange(await QueryAsync(factory, db => db.KbFaqs.CountAsync()), 6, 10);
        Assert.True(await QueryAsync(factory, db => db.Tasks.CountAsync()) >= 3);
        Assert.True(await QueryAsync(factory, db => db.Notifications.CountAsync()) >= 3);
        Assert.True(await QueryAsync(factory, db => db.CustomerNotes.CountAsync()) >= 5);
        Assert.True(await QueryAsync(factory, db => db.CustomerInteractions.CountAsync(i => i.Type == InteractionType.Ticket)) >= tickets.Count);

        // The demo staff can sign in with the seed password.
        Assert.False(string.IsNullOrEmpty(await factory.LoginAsync("sara@crm.com", CrmApiFactory.SuperAdminPassword)));
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        using var factory = new CrmApiFactory();
        Assert.True(await SeedAsync(factory));
        var tickets = await QueryAsync(factory, db => db.Tickets.CountAsync());
        var customers = await QueryAsync(factory, db => db.Customers.CountAsync());
        var users = await QueryAsync(factory, db => db.Users.CountAsync());

        Assert.False(await SeedAsync(factory));

        Assert.Equal(tickets, await QueryAsync(factory, db => db.Tickets.CountAsync()));
        Assert.Equal(customers, await QueryAsync(factory, db => db.Customers.CountAsync()));
        Assert.Equal(users, await QueryAsync(factory, db => db.Users.CountAsync()));
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Development", null)]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    [InlineData("Staging", true)]
    public async Task Seed_DoesNothing_WhenTheFlagIsOffOrTheEnvironmentIsNotDevelopment(string environment, bool? flag)
    {
        using var factory = new CrmApiFactory();

        Assert.False(await SeedAsync(factory, environment, flag));

        Assert.Equal(0, await QueryAsync(factory, db => db.Customers.CountAsync()));
        Assert.Equal(0, await QueryAsync(factory, db => db.Tickets.CountAsync()));
        Assert.Equal(0, await QueryAsync(factory, db => db.KbArticles.CountAsync()));
    }

    [Fact]
    public async Task Seed_AddsDataOnce_ToADatabaseThatAlreadyHasData()
    {
        using var factory = new CrmApiFactory();
        var existing = await factory.CreateUserAsync($"existing-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, "Agent");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            db.Customers.Add(Customer.Create("Real Customer", "real@example.com", null, factory.Time.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
        }

        Assert.True(await SeedAsync(factory));

        Assert.True(await QueryAsync(factory, db => db.Users.AnyAsync(u => u.Id == existing)));
        Assert.True(await QueryAsync(factory, db => db.Customers.AnyAsync(c => c.Email == "real@example.com")));
        Assert.InRange(await QueryAsync(factory, db => db.Tickets.CountAsync()), 280, 320);
    }

    [Fact]
    public async Task Seed_FeedsTheDashboardAndEveryReport()
    {
        using var factory = new CrmApiFactory();
        Assert.True(await SeedAsync(factory));
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);
        var range = $"from={today.AddDays(-60):yyyy-MM-dd}&to={today:yyyy-MM-dd}";

        var dashboard = (await client.GetFromJsonAsync<DashboardResponse>("/api/reports/dashboard"))!;
        Assert.True(dashboard.OpenTickets > 0);
        Assert.True(dashboard.AverageResponseMinutes > 0);
        Assert.True(dashboard.AverageCsat is >= 3.5 and <= 5);
        Assert.True(dashboard.CsatCount > 0);
        Assert.Contains(dashboard.TicketsPerDay, d => d.Count > 0);
        Assert.True(dashboard.TicketsByChannel.Count >= 5);

        var tickets = (await client.GetFromJsonAsync<TicketReportResponse>($"/api/reports/tickets?{range}"))!;
        Assert.InRange(tickets.Total, 280, 320);
        Assert.True(tickets.ByStatus.Count >= 4);
        Assert.True(tickets.ByCategory.Count >= 5);
        Assert.True(tickets.ByDay.Count(d => d.Count > 0) > 30);

        var sla = (await client.GetFromJsonAsync<SlaReportResponse>($"/api/reports/sla?{range}"))!;
        Assert.True(sla.Overall.Tickets > 0);
        Assert.True(sla.Overall.Response.Met > 0 && sla.Overall.Response.Breached > 0);
        Assert.True(sla.Overall.Resolution.Met > 0 && sla.Overall.Resolution.Breached > 0);

        var breaches = await client.GetFromJsonAsync<JsonElement>($"/api/reports/sla/breaches?{range}");
        Assert.True(breaches.GetProperty("totalCount").GetInt32() > 0);

        var csat = (await client.GetFromJsonAsync<CsatReportResponse>($"/api/reports/csat?{range}"))!;
        Assert.True(csat.TotalRatings > 20);
        Assert.True(csat.AverageRating >= 3.5);
        Assert.True(csat.ByAgent.Count >= 4);
        Assert.NotEmpty(csat.LowRatings);

        var agents = (await client.GetFromJsonAsync<AgentReportResponse>($"/api/reports/agents?{range}"))!;
        Assert.True(agents.Agents.Count >= 4);
        Assert.All(agents.Agents, a => Assert.True(a.TicketsHandled > 0));
        Assert.Contains(agents.Agents, a => a.AverageCsat is not null);
        Assert.True(agents.Agents.Select(a => a.TicketsHandled).Distinct().Count() > 2);
    }
}
