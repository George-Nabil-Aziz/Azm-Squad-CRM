using Crm.Application.Chat;
using Crm.Domain.Chat;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Chat;

/// <summary>EF Core storage of live chats and their messages.</summary>
public sealed class ChatSessionRepository(CrmDbContext db) : IChatSessionRepository
{
    public void Add(ChatSession session) => db.ChatSessions.Add(session);

    public Task<ChatSession?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.ChatSessions.Include(s => s.Messages).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ChatSession>> ListAsync(ChatStatus status, Guid? agentId, CancellationToken cancellationToken) =>
        await db.ChatSessions.AsNoTracking()
            .Where(s => s.Status == status && (agentId == null || s.AgentId == agentId))
            .OrderBy(s => s.StartedAt)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public static class ChatInfrastructureExtensions
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        return services;
    }
}
