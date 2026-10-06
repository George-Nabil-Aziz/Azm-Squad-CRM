using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Crm.Api.IntegrationTests.Notifications;

/// <summary>CRM-28 AC 1: the SignalR hub pushes a new assignment to the assignee in real time (test server, no network).</summary>
public class NotificationHubTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private HubConnection Connect(string? token)
    {
        factory.CreateClient(); // starts the test server
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
    }

    [Fact]
    public async Task AssigningATicket_PushesTheNotificationToTheAssigneeInRealTime()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var agentId = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        await using var connection = Connect(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("notification", payload => received.TrySetResult(payload));
        await connection.StartAsync();

        var customerId = await TicketArrange.CustomerAsync(supervisor);
        var ticket = await TicketArrange.TicketAsync(supervisor, customerId);
        await supervisor.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assigneeId = agentId });

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var notification = payload.GetProperty("notification");
        Assert.Equal("assignment", notification.GetProperty("type").GetString());
        Assert.Equal(ticket.Id, notification.GetProperty("ticketId").GetGuid());
        Assert.Equal(1, payload.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task AnotherUsersAssignment_IsNotPushedToMe()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var other = await factory.CreateUserAsync($"other-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Agent);
        var email = $"me-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        await using var connection = Connect(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("notification", payload => received.TrySetResult(payload));
        await connection.StartAsync();

        var customerId = await TicketArrange.CustomerAsync(supervisor);
        var ticket = await TicketArrange.TicketAsync(supervisor, customerId);
        await supervisor.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assigneeId = other });

        await Assert.ThrowsAsync<TimeoutException>(() => received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ConnectingWithoutAToken_IsRefused()
    {
        await using var connection = Connect(null);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync());
    }
}
