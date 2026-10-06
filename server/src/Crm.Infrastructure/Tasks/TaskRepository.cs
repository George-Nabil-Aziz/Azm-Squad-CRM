using Crm.Application.Tasks;
using Crm.Domain.Tasks;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tasks;

/// <summary>EF Core storage of tasks (CRM-31).</summary>
public sealed class TaskRepository(CrmDbContext db) : ITaskRepository
{
    public void Add(WorkTask task) => db.Tasks.Add(task);

    public Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.Tickets.AnyAsync(t => t.Id == ticketId, cancellationToken);

    public Task<WorkTask?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) =>
        db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId, cancellationToken);

    public async Task<IReadOnlyList<TaskResponse>> ListAsync(Guid ownerId, bool done, CancellationToken cancellationToken) =>
        Map(await Rows().Where(r => r.Task.OwnerId == ownerId && (r.Task.CompletedAt != null) == done)
            .OrderBy(r => r.Task.DueAt).ThenBy(r => r.Task.CreatedAt)
            .ToListAsync(cancellationToken));

    public async Task<TaskResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await Rows().Where(r => r.Task.Id == id).ToListAsync(cancellationToken)).FirstOrDefault();

    public async Task<IReadOnlyList<WorkTask>> ListDueReminderCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        await db.Tasks
            .Where(t => t.CompletedAt == null && t.ReminderSentAt == null && t.DueAt <= utcNow)
            .OrderBy(t => t.DueAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static IReadOnlyList<TaskResponse> Map(List<Row> rows) =>
        [.. rows.Select(r => TaskService.ToResponse(r.Task, r.TicketNumber is { } n ? Ticket.FormatNumber(n) : null))];

    private IQueryable<Row> Rows() =>
        from task in db.Tasks.AsNoTracking()
        join ticket in db.Tickets.AsNoTracking() on task.TicketId equals ticket.Id into tickets
        from ticket in tickets.DefaultIfEmpty()
        select new Row { Task = task, TicketNumber = ticket != null ? (int?)ticket.Number : null };

    private sealed class Row
    {
        public required WorkTask Task { get; init; }

        public int? TicketNumber { get; init; }
    }
}
