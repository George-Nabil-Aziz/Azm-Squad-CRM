using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Notifications;
using Crm.Domain.Notifications;
using Crm.Domain.Tasks;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tasks;

/// <summary>Body of POST /api/tasks: title and due time (UTC, in the future) are required; the ticket is optional.</summary>
public sealed record CreateTaskRequest(string? Title, string? Description, DateTime? DueAt, Guid? TicketId);

/// <summary>A task as the API returns it. <c>TicketNumber</c> is "TKT-000001" for a linked ticket.</summary>
public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    DateTime DueAt,
    Guid? TicketId,
    string? TicketNumber,
    bool IsDone,
    DateTime? CompletedAt,
    DateTime CreatedAt);

/// <summary>GET /api/tasks query: <c>status</c> "open" (default) or "done".</summary>
public sealed record ListTasksQuery(string? Status);

/// <summary>Task storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ITaskRepository
{
    void Add(WorkTask task);

    Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>The tracked task when it belongs to the owner, otherwise null.</summary>
    Task<WorkTask?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>The owner's open (or done) tasks, by due time.</summary>
    Task<IReadOnlyList<TaskResponse>> ListAsync(Guid ownerId, bool done, CancellationToken cancellationToken);

    Task<TaskResponse?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked open tasks whose due time has come and that were not reminded yet.</summary>
    Task<IReadOnlyList<WorkTask>> ListDueReminderCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>My tasks. <c>ValidationException</c> 400, <c>NotFoundException</c> 404 for an unknown or foreign task.</summary>
public interface ITaskService
{
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskResponse>> ListAsync(ListTasksQuery query, CancellationToken cancellationToken);

    Task<TaskResponse> MarkDoneAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(_ => TaskText.TitleRequired).MaximumLength(WorkTask.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(WorkTask.DescriptionMaxLength);
        RuleFor(x => x.DueAt).NotNull().WithMessage(_ => TaskText.DueRequired);
    }
}

public sealed class ListTasksQueryValidator : AbstractValidator<ListTasksQuery>
{
    public ListTasksQueryValidator() =>
        RuleFor(x => x.Status)
            .Must(s => s is "open" or "done").WithMessage(_ => TaskText.StatusInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
}

public sealed class TaskService(
    ITaskRepository tasks,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CreateTaskRequest> createValidator,
    IValidator<ListTasksQuery> listValidator) : ITaskService
{
    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dueAt = DateTime.SpecifyKind(request.DueAt!.Value.ToUniversalTime(), DateTimeKind.Utc);
        if (dueAt <= now)
        {
            throw Field("dueAt", TaskText.DueInPast); // AC 2
        }

        if (request.TicketId is { } ticketId && !await tasks.TicketExistsAsync(ticketId, cancellationToken))
        {
            throw Field("ticketId", TaskText.TicketNotFound);
        }

        var task = WorkTask.Create(Owner(), request.Title!, request.Description, dueAt, request.TicketId, now);
        tasks.Add(task);
        await tasks.SaveChangesAsync(cancellationToken);
        return await tasks.GetAsync(task.Id, cancellationToken) ?? ToResponse(task, null);
    }

    public async Task<IReadOnlyList<TaskResponse>> ListAsync(ListTasksQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        return await tasks.ListAsync(Owner(), query.Status == "done", cancellationToken);
    }

    public async Task<TaskResponse> MarkDoneAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await tasks.FindAsync(id, Owner(), cancellationToken) ?? throw new NotFoundException(TaskText.NotFound);
        if (task.MarkDone(timeProvider.GetUtcNow().UtcDateTime))
        {
            await tasks.SaveChangesAsync(cancellationToken);
        }

        return await tasks.GetAsync(id, cancellationToken) ?? ToResponse(task, null);
    }

    public static TaskResponse ToResponse(WorkTask task, string? ticketNumber) => new(
        task.Id, task.Title, task.Description, task.DueAt, task.TicketId, ticketNumber, task.IsDone, task.CompletedAt, task.CreatedAt);

    private Guid Owner() => currentUser.UserId ?? throw new UnauthorizedException(TaskText.NotFound);

    private static ValidationException Field(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>
/// Sends the reminder of tasks that are due (recurring job <c>task-reminders</c>, every minute). A plain class: Hangfire
/// calls <see cref="RunAsync"/>, tests call it directly. Returns how many reminders were sent.
/// </summary>
public sealed class TaskReminderJob(ITaskRepository tasks, INotificationDispatcher notifications, TimeProvider timeProvider)
{
    public const int BatchSize = 500;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var due = await tasks.ListDueReminderCandidatesAsync(now, BatchSize, cancellationToken);
        foreach (var task in due)
        {
            // The key makes a retry after a failed save harmless: the dispatcher never notifies the same event twice.
            await notifications.NotifyAsync(
                new NotificationRequest(NotificationType.TaskReminder, $"task-reminder:{task.Id}", [task.OwnerId], null, task.TicketId, 0, task.Title),
                cancellationToken);
            task.MarkReminderSent(now);
        }

        if (due.Count > 0)
        {
            await tasks.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }
}

/// <summary>User-facing text of the tasks feature, in the request language.</summary>
public static class TaskText
{
    public static string TitleRequired => LocalizedText.Get("Enter a title.", "أدخل عنواناً.");

    public static string DueRequired => LocalizedText.Get("Enter a due date.", "أدخل تاريخ الاستحقاق.");

    public static string DueInPast => LocalizedText.Get("The due date must be in the future.", "يجب أن يكون تاريخ الاستحقاق في المستقبل.");

    public static string TicketNotFound => LocalizedText.Get("The ticket was not found.", "التذكرة غير موجودة.");

    public static string NotFound => LocalizedText.Get("The task was not found.", "المهمة غير موجودة.");

    public static string StatusInvalid => LocalizedText.Get("Choose open or done.", "اختر مفتوحة أو منجزة.");
}
