using System.Collections.Concurrent;

namespace Crm.Application.Chat;

/// <summary>Connections per agent, kept in memory. Register as a singleton.</summary>
public sealed class InMemoryAgentPresence : IAgentPresence
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new(StringComparer.Ordinal);

    public void Connected(Guid agentId, string connectionId) => _connections[connectionId] = agentId;

    public void Disconnected(string connectionId) => _connections.TryRemove(connectionId, out _);

    public bool HasOnlineAgents => !_connections.IsEmpty;

    public IReadOnlyCollection<Guid> OnlineAgents => [.. _connections.Values.Distinct()];
}
