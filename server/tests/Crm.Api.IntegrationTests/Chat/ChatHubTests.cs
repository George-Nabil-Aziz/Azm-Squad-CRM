using System.Diagnostics;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Chat;

/// <summary>CRM-56: the live chat hub (test server, no network).</summary>
public class ChatHubTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AChatStartedFromTheWidget_AppearsToTheOnlineAgent_InRealTime()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);

        var chat = await ChatTestSupport.StartChatAsync(host, "Real Time Visitor");

        var started = await agent.Started.Task.WaitAsync(Wait);
        Assert.Equal(chat.Id, started.GetProperty("id").GetGuid());
        Assert.Equal("Real Time Visitor", started.GetProperty("visitorName").GetString());
        Assert.Equal("waiting", started.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Messages_AreDeliveredBothWays_InUnderOneSecond()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);
        var chat = await ChatTestSupport.StartChatAsync(host, "Two Way Visitor");
        await agent.Started.Task.WaitAsync(Wait);
        await agent.Connection.InvokeAsync<JsonElement>("Accept", chat.Id);

        await using var visitor = ChatTestSupport.Connect(host, session: chat.Id, visitorToken: chat.Token);
        var visitorGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        visitor.On<JsonElement>("MessageReceived", message =>
        {
            if (message.GetProperty("sender").GetString() == "agent")
            {
                visitorGot.TrySetResult(message);
            }
        });
        await visitor.StartAsync();

        // Warm-up in both directions (first calls pay for JIT and connection set-up), then measure.
        await visitor.InvokeAsync<JsonElement>("Send", chat.Id, "warm-up");
        await WaitForMessageAsync(agent, "warm-up");

        var toAgent = Stopwatch.StartNew();
        await visitor.InvokeAsync<JsonElement>("Send", chat.Id, "My printer is broken");
        await WaitForMessageAsync(agent, "My printer is broken");
        Assert.True(toAgent.Elapsed < TimeSpan.FromSeconds(1), $"visitor to agent took {toAgent.Elapsed}");

        var toVisitor = Stopwatch.StartNew();
        await agent.Connection.InvokeAsync<JsonElement>("Send", chat.Id, "Let me check");
        var received = await visitorGot.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(toVisitor.Elapsed < TimeSpan.FromSeconds(1), $"agent to visitor took {toVisitor.Elapsed}");
        Assert.Equal(("agent", "Let me check"), (received.GetProperty("sender").GetString(), received.GetProperty("body").GetString()));
    }

    [Fact]
    public async Task EndingTheChat_SavesTheTranscriptAsATicket()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);
        var name = $"Transcript Visitor {Guid.NewGuid():N}";
        var chat = await ChatTestSupport.StartChatAsync(host, name, "Where is my order?");
        await agent.Started.Task.WaitAsync(Wait);
        await agent.Connection.InvokeAsync<JsonElement>("Accept", chat.Id);
        await agent.Connection.InvokeAsync<JsonElement>("Send", chat.Id, "It ships tomorrow");

        var ended = await agent.Connection.InvokeAsync<JsonElement>("End", chat.Id);

        Assert.Equal("ended", ended.GetProperty("status").GetString());
        Assert.Matches("^TKT-[0-9]{6}$", ended.GetProperty("ticketNumber").GetString());
        await agent.Ended.Task.WaitAsync(Wait);
        using var scope = host.Services.CreateScope();
        var ticket = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.AsNoTracking()
            .SingleAsync(t => t.Subject == $"Chat with {name}");
        Assert.Equal(Crm.Domain.Tickets.TicketChannel.Chat, ticket.Channel);
        Assert.Contains("Where is my order?", ticket.Description);
        Assert.Contains("It ships tomorrow", ticket.Description);
    }

    [Fact]
    public async Task AVisitorConnection_WithAWrongToken_IsRefused()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);
        var chat = await ChatTestSupport.StartChatAsync(host, "Wrong Token Visitor");
        await using var visitor = ChatTestSupport.Connect(host, session: chat.Id, visitorToken: "not-the-token");

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => visitor.StartAsync());
    }

    [Fact]
    public async Task ASignedInUserWithoutChatHandle_IsRefused()
    {
        var host = ChatTestSupport.FreshHost(factory);
        var email = $"no-chat-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword);
        await using var connection = ChatTestSupport.Connect(host, await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync());
    }

    [Fact]
    public async Task AnotherAgent_CannotWriteInAChatItDidNotAccept()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var first = await ChatTestSupport.ConnectAgentAsync(factory, host);
        await using var second = await ChatTestSupport.ConnectAgentAsync(factory, host);
        var chat = await ChatTestSupport.StartChatAsync(host, "Shared Visitor");
        await first.Started.Task.WaitAsync(Wait);
        await first.Connection.InvokeAsync<JsonElement>("Accept", chat.Id);

        await Assert.ThrowsAsync<HubException>(() => second.Connection.InvokeAsync<JsonElement>("Send", chat.Id, "Hi"));
        await Assert.ThrowsAsync<HubException>(() => second.Connection.InvokeAsync<JsonElement>("Accept", chat.Id));
    }

    private static async Task WaitForMessageAsync(AgentConnection agent, string body)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            if (agent.Messages.Any(m => m.GetProperty("body").GetString() == body))
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"The agent never received '{body}'.");
    }
}
