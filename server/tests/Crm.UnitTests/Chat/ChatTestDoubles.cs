using Crm.Application.Chat;
using Crm.Domain.Chat;

namespace Crm.UnitTests.Chat;

internal sealed class FakeChatRepository : IChatSessionRepository
{
    public List<ChatSession> Sessions { get; } = [];

    public void Add(ChatSession session) => Sessions.Add(session);

    public Task<ChatSession?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<ChatSession>> ListAsync(ChatStatus status, Guid? agentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ChatSession>>(
            [.. Sessions.Where(s => s.Status == status && (agentId is null || s.AgentId == agentId)).OrderBy(s => s.StartedAt)]);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeChatNotifier : IChatNotifier
{
    public List<ChatSessionResponse> Started { get; } = [];

    public List<ChatSessionResponse> Accepted { get; } = [];

    public List<ChatMessageResponse> Messages { get; } = [];

    public List<ChatSessionResponse> Ended { get; } = [];

    public Task ChatStartedAsync(ChatSessionResponse session, CancellationToken cancellationToken)
    {
        Started.Add(session);
        return Task.CompletedTask;
    }

    public Task ChatAcceptedAsync(ChatSessionResponse session, CancellationToken cancellationToken)
    {
        Accepted.Add(session);
        return Task.CompletedTask;
    }

    public Task MessageAsync(ChatMessageResponse message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task EndedAsync(ChatSessionResponse session, CancellationToken cancellationToken)
    {
        Ended.Add(session);
        return Task.CompletedTask;
    }
}
