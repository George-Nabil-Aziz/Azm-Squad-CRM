using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Channels;
using Crm.Application.Sla;
using Crm.Infrastructure.Channels.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeKit;

namespace Crm.Api.IntegrationTests.Notifications;

/// <summary>CRM-28: in-app notifications, read state, assignment notices and SLA emails against the real database and API.</summary>
public class NotificationsApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record NotificationBody(Guid Id, string Type, Guid? TicketId, string? TicketNumber, int Level, string? Text, DateTime CreatedAt, DateTime? ReadAt);

    public sealed record PageBody(NotificationBody[] Items, int Page, int PageSize, int TotalCount);

    private sealed record CountBody(int Count);

    private sealed class FakeSmtpTransport : ISmtpTransport
    {
        public List<MimeMessage> Sent { get; } = [];

        public Task SendAsync(MimeMessage message, EmailChannelOptions.SmtpSettings settings, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>A staff user (agent) with a signed-in client.</summary>
    private async Task<(Guid Id, string Email, HttpClient Client)> AgentAsync()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        return (id, email, factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword)));
    }

    private static async Task<Guid> AssignNewTicketAsync(HttpClient supervisor, Guid assigneeId, string priority = "high")
    {
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        var ticket = await TicketArrange.TicketAsync(supervisor, customerId, priority: priority);
        var response = await supervisor.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assigneeId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ticket.Id;
    }

    private static Task<PageBody?> ListAsync(HttpClient client, string query = "") =>
        client.GetFromJsonAsync<PageBody>("/api/notifications" + query);

    [Fact]
    public async Task Assigning_NotifiesTheAssignee_WhoCanReadItMarkItReadAndSeeTheUnreadCount()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await AgentAsync();

        var ticketId = await AssignNewTicketAsync(supervisor, agent.Id);

        var page = (await ListAsync(agent.Client))!;
        var notification = Assert.Single(page.Items);
        Assert.Equal(("assignment", (Guid?)ticketId, (DateTime?)null), (notification.Type, notification.TicketId, notification.ReadAt));
        Assert.StartsWith("TKT-", notification.TicketNumber);
        Assert.Equal(1, (await agent.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);

        var read = await agent.Client.PostAsync($"/api/notifications/{notification.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.NotNull((await read.Content.ReadFromJsonAsync<NotificationBody>())!.ReadAt);
        Assert.Equal(0, (await agent.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);
        Assert.Empty((await ListAsync(agent.Client, "?unreadOnly=true"))!.Items);
        Assert.Single((await ListAsync(agent.Client))!.Items);
    }

    [Fact]
    public async Task MarkAllRead_ReadsEverything_AndForeignNotificationsAre404()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await AgentAsync();
        var other = await AgentAsync();
        await AssignNewTicketAsync(supervisor, agent.Id);
        await AssignNewTicketAsync(supervisor, agent.Id);
        await AssignNewTicketAsync(supervisor, other.Id);
        var foreign = (await ListAsync(other.Client))!.Items.Single();

        Assert.Equal(HttpStatusCode.NotFound, (await agent.Client.PostAsync($"/api/notifications/{foreign.Id}/read", null)).StatusCode);
        Assert.Equal(2, (await agent.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);

        var all = await agent.Client.PostAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(0, (await agent.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);
        Assert.Equal(1, (await other.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);
    }

    [Fact]
    public async Task SelfAssignment_NotifiesNobody()
    {
        var agent = await AgentAsync();
        var customerId = await TicketArrange.CustomerAsync(agent.Client);
        var ticket = await TicketArrange.TicketAsync(agent.Client, customerId);

        var response = await agent.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assigneeId = agent.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ListAsync(agent.Client))!.Items);
    }

    [Fact]
    public async Task Notifications_RequireSignIn()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications/unread-count")).StatusCode);
    }

    [Fact]
    public async Task SlaWarning_SendsAnInAppNotificationAndAnEmail_OncePerEvent()
    {
        var smtp = new FakeSmtpTransport();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Channels:Email:FromAddress"] = "support@azm.example",
                ["Channels:Email:Smtp:Host"] = "smtp.example.test",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISmtpTransport>();
                services.AddSingleton<ISmtpTransport>(smtp);
            });
        });
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await AgentAsync();
        var ticketId = await AssignNewTicketAsync(supervisor, agent.Id);

        factory.Time.Advance(TimeSpan.FromMinutes(97)); // High: 120 min response, warning at 96 min
        await RunJobAsync(app.Services);
        await RunJobAsync(app.Services);

        var fresh = await SignInAgainAsync(agent.Email); // the 97 minutes also expired the first token
        var warning = Assert.Single((await ListAsync(fresh))!.Items, n => n.Type == "slaWarning"); // AC 2 (in-app) and AC 4 (once)
        Assert.Equal(ticketId, warning.TicketId);
        var mail = Assert.Single(smtp.Sent, m => m.To.Mailboxes.Any(a => a.Address == agent.Email)); // AC 2 (email)
        Assert.Contains("SLA", mail.Subject);
        Assert.Contains(warning.TicketNumber!, mail.Subject);
    }

    [Fact]
    public async Task SlaWarning_WithoutSmtpSettings_StillNotifiesInApp()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await AgentAsync();
        await AssignNewTicketAsync(supervisor, agent.Id);

        factory.Time.Advance(TimeSpan.FromMinutes(97));
        await RunJobAsync(factory.Services);

        Assert.Single((await ListAsync(await SignInAgainAsync(agent.Email)))!.Items, n => n.Type == "slaWarning");
    }

    private async Task<HttpClient> SignInAgainAsync(string email) =>
        factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

    private static async Task RunJobAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SlaMonitorJob>().RunAsync(CancellationToken.None);
    }
}
