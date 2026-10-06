using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Notifications;
using Crm.Application.Auth;
using Crm.Application.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tasks;

/// <summary>CRM-31: tasks API and the reminder job against the real database.</summary>
public class TasksApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record TaskBody(Guid Id, string Title, DateTime DueAt, Guid? TicketId, string? TicketNumber, bool IsDone);

    private DateTime InHours(int hours) => factory.Time.GetUtcNow().UtcDateTime.AddHours(hours);

    private async Task<(Guid Id, string Email, HttpClient Client)> AgentAsync()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        return (id, email, factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword)));
    }

    [Fact]
    public async Task Create_WithTitleAndDueDate_Returns201()
    {
        var agent = await AgentAsync();

        var response = await agent.Client.PostAsJsonAsync("/api/tasks", new { title = "Call Nour", dueAt = InHours(2) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // AC 1
        var task = (await response.Content.ReadFromJsonAsync<TaskBody>())!;
        Assert.Equal(("Call Nour", false), (task.Title, task.IsDone));
    }

    [Fact]
    public async Task Create_WithADueDateInThePast_Returns400()
    {
        var agent = await AgentAsync();

        var response = await agent.Client.PostAsJsonAsync("/api/tasks", new { title = "Too late", dueAt = InHours(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // AC 2
        Assert.Contains("dueAt", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Done_RemovesTheTaskFromMyOpenTasks_AndOthersCannotTouchIt()
    {
        var agent = await AgentAsync();
        var other = await AgentAsync();
        var task = (await (await agent.Client.PostAsJsonAsync("/api/tasks", new { title = "Follow up", dueAt = InHours(3) }))
            .Content.ReadFromJsonAsync<TaskBody>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync($"/api/tasks/{task.Id}/done", null)).StatusCode);
        Assert.Empty((await other.Client.GetFromJsonAsync<TaskBody[]>("/api/tasks"))!);
        Assert.Single((await agent.Client.GetFromJsonAsync<TaskBody[]>("/api/tasks"))!);

        Assert.Equal(HttpStatusCode.OK, (await agent.Client.PostAsync($"/api/tasks/{task.Id}/done", null)).StatusCode);

        Assert.Empty((await agent.Client.GetFromJsonAsync<TaskBody[]>("/api/tasks"))!); // AC 4
        Assert.Single((await agent.Client.GetFromJsonAsync<TaskBody[]>("/api/tasks?status=done"))!);
    }

    [Fact]
    public async Task ATaskLinkedToATicket_ShowsTheTicketNumber()
    {
        var agent = await AgentAsync();
        var ticket = await Tickets.TicketArrange.TicketAsync(agent.Client, await Tickets.TicketArrange.CustomerAsync(agent.Client));

        var created = (await (await agent.Client.PostAsJsonAsync("/api/tasks", new { title = "Check", dueAt = InHours(1), ticketId = ticket.Id }))
            .Content.ReadFromJsonAsync<TaskBody>())!;

        Assert.Equal((ticket.Id, ticket.Number), (created.TicketId, created.TicketNumber));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await agent.Client.PostAsJsonAsync("/api/tasks", new { title = "x", dueAt = InHours(1), ticketId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/tasks")).StatusCode);

    [Fact]
    public async Task Reminder_IsSentAtTheDueTime_Once_ToTheOwner()
    {
        var agent = await AgentAsync();
        await agent.Client.PostAsJsonAsync("/api/tasks", new { title = "Call Nour", dueAt = InHours(1) });

        await RunJobAsync();
        var fresh = factory.CreateAuthenticatedClient(await factory.LoginAsync(agent.Email, CrmApiFactory.TestUserPassword));
        Assert.Empty((await fresh.GetFromJsonAsync<NotificationsApiTests.PageBody>("/api/notifications"))!.Items); // not due yet

        factory.Time.Advance(TimeSpan.FromMinutes(61));
        await RunJobAsync();
        await RunJobAsync();

        fresh = factory.CreateAuthenticatedClient(await factory.LoginAsync(agent.Email, CrmApiFactory.TestUserPassword));
        var reminder = Assert.Single((await fresh.GetFromJsonAsync<NotificationsApiTests.PageBody>("/api/notifications"))!.Items); // AC 3
        Assert.Equal(("taskReminder", "Call Nour"), (reminder.Type, reminder.Text));
    }

    private async Task RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TaskReminderJob>().RunAsync(CancellationToken.None);
    }
}
