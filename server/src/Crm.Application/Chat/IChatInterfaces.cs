using Crm.Domain.Chat;

namespace Crm.Application.Chat;

/// <summary>Storage of chats (EF Core in Infrastructure).</summary>
public interface IChatSessionRepository
{
    void Add(ChatSession session);

    /// <summary>The chat with its messages, oldest first; null when unknown.</summary>
    Task<ChatSession?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Chats in a status, oldest first (messages not loaded). <c>agentId</c> limits to the chats of that agent.</summary>
    Task<IReadOnlyList<ChatSession>> ListAsync(ChatStatus status, Guid? agentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Pushes chat events to the connected clients (SignalR in Crm.Api; tests use a recording fake).</summary>
public interface IChatNotifier
{
    /// <summary>A new waiting chat goes to every online agent.</summary>
    Task ChatStartedAsync(ChatSessionResponse session, CancellationToken cancellationToken);

    /// <summary>An agent took the chat: the visitor and every agent are told (the chat leaves the queue).</summary>
    Task ChatAcceptedAsync(ChatSessionResponse session, CancellationToken cancellationToken);

    /// <summary>A message goes to both sides of the chat.</summary>
    Task MessageAsync(ChatMessageResponse message, CancellationToken cancellationToken);

    /// <summary>The chat ended (transcript saved): both sides and every agent are told.</summary>
    Task EndedAsync(ChatSessionResponse session, CancellationToken cancellationToken);
}

/// <summary>Which agents are connected to the chat hub right now. In memory (per server instance).</summary>
public interface IAgentPresence
{
    void Connected(Guid agentId, string connectionId);

    void Disconnected(string connectionId);

    bool HasOnlineAgents { get; }

    IReadOnlyCollection<Guid> OnlineAgents { get; }
}

/// <summary>
/// Live chat use cases (CRM-56). Failures: <c>ValidationException</c> 400, <c>ConflictException</c> 409 (no agent online,
/// already accepted, ended), <c>ForbiddenException</c> 403, <c>NotFoundException</c> 404, <c>RateLimitExceededException</c> 429.
/// </summary>
public interface IChatService
{
    bool IsAvailable { get; }

    Task<StartChatResponse> StartAsync(StartChatRequest request, string? remoteIp, CancellationToken cancellationToken);

    /// <summary>True when the token belongs to the chat (constant-time comparison of the hashes).</summary>
    Task<bool> AuthorizeVisitorAsync(Guid sessionId, string? token, CancellationToken cancellationToken);

    Task<ChatSessionResponse> AcceptAsync(Guid sessionId, Guid agentId, string agentName, CancellationToken cancellationToken);

    Task<ChatMessageResponse> SendAsync(Guid sessionId, ChatParticipant sender, string? body, CancellationToken cancellationToken);

    /// <summary>Ends the chat and saves the transcript as a ticket; ending an ended chat returns it unchanged.</summary>
    Task<ChatSessionResponse> EndAsync(Guid sessionId, ChatParticipant by, CancellationToken cancellationToken);

    /// <summary>"waiting" (the queue) or "active" (the agent's own chats).</summary>
    Task<IReadOnlyList<ChatSessionResponse>> ListAsync(string? status, Guid agentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessageResponse>> MessagesAsync(Guid sessionId, CancellationToken cancellationToken);
}
