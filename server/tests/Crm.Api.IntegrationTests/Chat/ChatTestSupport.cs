using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Crm.Api.IntegrationTests.Chat;

/// <summary>Helpers of the live chat tests: hub connections over the test server (long polling) and a fresh host per test class need.</summary>
internal static class ChatTestSupport
{
    /// <summary>A new host (own in-memory agent presence, same database).</summary>
    public static WebApplicationFactory<Program> FreshHost(CrmApiFactory factory) => factory.WithWebHostBuilder(_ => { });

    public static HubConnection Connect(WebApplicationFactory<Program> host, string? accessToken = null, Guid? session = null, string? visitorToken = null)
    {
        host.CreateClient(); // starts the test server
        var query = session is null ? string.Empty : $"?session={session}&token={visitorToken}";
        return new HubConnectionBuilder()
            .WithUrl(new Uri(host.Server.BaseAddress, "/hubs/chat" + query), options =>
            {
                options.HttpMessageHandlerFactory = _ => host.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();
    }

    /// <summary>Creates an agent, signs in and connects to the hub; events are collected in <see cref="AgentConnection"/>.</summary>
    public static async Task<AgentConnection> ConnectAgentAsync(CrmApiFactory factory, WebApplicationFactory<Program> host)
    {
        var email = $"chat-agent-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        var connection = Connect(host, await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
        var agent = new AgentConnection(connection);
        await connection.StartAsync();
        return agent;
    }

    public static async Task<StartedChat> StartChatAsync(WebApplicationFactory<Program> host, string name, string message = "Hello")
    {
        var response = await host.CreateClient().PostAsJsonAsync(
            "/api/public/chat/sessions", new { name, email = $"{Guid.NewGuid():N}@visitor.example", message });
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
        return new StartedChat(body.GetProperty("session").GetProperty("id").GetGuid(), body.GetProperty("visitorToken").GetString()!);
    }
}

internal sealed record StartedChat(Guid Id, string Token);

/// <summary>An agent's hub connection that records the chat events it receives.</summary>
internal sealed class AgentConnection : IAsyncDisposable
{
    public AgentConnection(HubConnection connection)
    {
        Connection = connection;
        connection.On<JsonElement>("ChatStarted", payload => Started.TrySetResult(payload));
        connection.On<JsonElement>("MessageReceived", payload => Messages.Enqueue(payload));
        connection.On<JsonElement>("ChatEnded", payload => Ended.TrySetResult(payload));
    }

    public HubConnection Connection { get; }

    public TaskCompletionSource<JsonElement> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<JsonElement> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public System.Collections.Concurrent.ConcurrentQueue<JsonElement> Messages { get; } = new();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
