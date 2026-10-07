using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Auth;
using Crm.Application.Chat;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Chat;
using Microsoft.AspNetCore.SignalR;

namespace Crm.Api.Chat;

/// <summary>
/// Live chat (CRM-56) at <see cref="Path"/>. Two kinds of connection, both checked before the hub is reached by
/// <see cref="ChatHubAccessMiddleware"/>: agents (JWT with <c>chat.handle</c>, token in the <c>access_token</c> query string) and
/// visitors (query <c>session</c> + <c>token</c> returned when the chat started). Agents are in group <see cref="AgentsGroup"/>
/// and in the group of every chat they accepted; a visitor is in the group of their chat.
/// </summary>
public sealed class ChatHub(IChatService chat, IAgentPresence presence) : Hub
{
    public const string Path = "/hubs/chat";
    public const string AgentsGroup = "agents";

    // Server → client events.
    public const string ChatStartedEvent = "ChatStarted";
    public const string ChatAcceptedEvent = "ChatAccepted";
    public const string MessageReceivedEvent = "MessageReceived";
    public const string ChatEndedEvent = "ChatEnded";

    private const string VisitorSessionKey = "visitorSession";

    public static string ChatGroup(Guid sessionId) => $"chat-{sessionId:N}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated == true && AgentId() is { } agentId)
        {
            presence.Connected(agentId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, AgentsGroup);
            foreach (var session in await chat.ListAsync("active", agentId, Context.ConnectionAborted))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroup(session.Id)); // reconnect: keep receiving
            }
        }
        else if (Guid.TryParse(Context.GetHttpContext()?.Request.Query["session"], out var sessionId))
        {
            Context.Items[VisitorSessionKey] = sessionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroup(sessionId));
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Disconnected(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Agent takes a waiting chat.</summary>
    public async Task<ChatSessionResponse> Accept(Guid sessionId)
    {
        var agentId = AgentId() ?? throw new HubException(Application.Common.Localization.ErrorText.Forbidden);
        var session = await Run(() => chat.AcceptAsync(sessionId, agentId, AgentName(), Context.ConnectionAborted));
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroup(sessionId));
        return session;
    }

    public Task<ChatMessageResponse> Send(Guid sessionId, string? body) =>
        Run(() => chat.SendAsync(sessionId, Participant(sessionId), body, Context.ConnectionAborted));

    public Task<ChatSessionResponse> End(Guid sessionId) =>
        Run(() => chat.EndAsync(sessionId, Participant(sessionId), Context.ConnectionAborted));

    /// <summary>The messages so far (a visitor who reconnected, an agent opening the chat).</summary>
    public Task<IReadOnlyList<ChatMessageResponse>> History(Guid sessionId)
    {
        _ = Participant(sessionId); // only the chat's own visitor or an agent
        return Run(() => chat.MessagesAsync(sessionId, Context.ConnectionAborted));
    }

    private ChatParticipant Participant(Guid sessionId)
    {
        if (AgentId() is { } agentId)
        {
            return new ChatParticipant(ChatSender.Agent, agentId, AgentName());
        }

        if (Context.Items.TryGetValue(VisitorSessionKey, out var own) && own is Guid ownId && ownId == sessionId)
        {
            return new ChatParticipant(ChatSender.Visitor, null, null);
        }

        throw new HubException(Application.Common.Localization.ErrorText.Forbidden);
    }

    private Guid? AgentId() =>
        Context.User?.Identity?.IsAuthenticated == true && Guid.TryParse(Context.User.FindFirstValue(AuthClaimTypes.UserId), out var id) ? id : null;

    private string AgentName() =>
        Context.User?.FindFirstValue(AuthClaimTypes.Name) ?? Context.User?.FindFirstValue(AuthClaimTypes.Email) ?? string.Empty;

    /// <summary>Business failures reach the client as readable <see cref="HubException"/> messages (any other exception stays hidden).</summary>
    private static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException exception)
        {
            throw new HubException(string.Join(' ', exception.Errors.SelectMany(pair => pair.Value)));
        }
        catch (Exception exception) when (exception is ConflictException or ForbiddenException or NotFoundException)
        {
            throw new HubException(exception.Message);
        }
    }
}

/// <summary>Pushes chat events to the connected agents and visitors.</summary>
public sealed class SignalRChatNotifier(IHubContext<ChatHub> hub) : IChatNotifier
{
    public Task ChatStartedAsync(ChatSessionResponse session, CancellationToken cancellationToken) =>
        hub.Clients.Group(ChatHub.AgentsGroup).SendAsync(ChatHub.ChatStartedEvent, session, cancellationToken);

    public Task ChatAcceptedAsync(ChatSessionResponse session, CancellationToken cancellationToken) =>
        hub.Clients.Groups(ChatHub.AgentsGroup, ChatHub.ChatGroup(session.Id)).SendAsync(ChatHub.ChatAcceptedEvent, session, cancellationToken);

    public Task MessageAsync(ChatMessageResponse message, CancellationToken cancellationToken) =>
        hub.Clients.Group(ChatHub.ChatGroup(message.SessionId)).SendAsync(ChatHub.MessageReceivedEvent, message, cancellationToken);

    public Task EndedAsync(ChatSessionResponse session, CancellationToken cancellationToken) =>
        hub.Clients.Groups(ChatHub.AgentsGroup, ChatHub.ChatGroup(session.Id)).SendAsync(ChatHub.ChatEndedEvent, session, cancellationToken);
}

/// <summary>
/// Refuses a chat hub request (negotiate and transport) before SignalR sees it: 401 / 403 for a signed-in user without
/// <c>chat.handle</c>, 401 for an anonymous caller whose <c>session</c> + <c>token</c> do not match a chat.
/// </summary>
public sealed class ChatHubAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IChatService chat)
    {
        // Later requests of an open connection (long-poll / send) carry the unguessable connection id the server issued after this
        // check passed at negotiate; only the first request of a connection is checked.
        if (!string.IsNullOrEmpty(context.Request.Query["id"]))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var roles = context.User.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value);
            if (!RolePermissions.HasPermission(roles, Permissions.ChatHandle))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        else if (!Guid.TryParse(context.Request.Query["session"], out var sessionId)
                 || !await chat.AuthorizeVisitorAsync(sessionId, context.Request.Query["token"], context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
