using Crm.Application.Common.Exceptions;
using Crm.Application.Notifications;
using Crm.Application.Tasks;
using Crm.Application.Tickets;
using Crm.Domain.Notifications;
using Crm.Domain.Tasks;
using Crm.UnitTests.Notifications;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Tasks;

internal sealed class FakeTaskRepository : ITaskRepository
{
    public List<WorkTask> Tasks { get; } = [];

    public HashSet<Guid> KnownTickets { get; } = [];

    public int SaveCount { get; private set; }

    public void Add(WorkTask task) => Tasks.Add(task);

    public Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(KnownTickets.Contains(ticketId));

    public Task<WorkTask?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id && t.OwnerId == ownerId));

    public Task<IReadOnlyList<TaskResponse>> ListAsync(Guid ownerId, bool done, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TaskResponse>>([.. Tasks
            .Where(t => t.OwnerId == ownerId && t.IsDone == done)
            .OrderBy(t => t.DueAt).ThenBy(t => t.CreatedAt)
            .Select(t => TaskService.ToResponse(t, null))]);

    public Task<TaskResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id) is { } task ? TaskService.ToResponse(task, null) : null);

    public Task<IReadOnlyList<WorkTask>> ListDueReminderCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkTask>>([.. Tasks.Where(t => !t.IsDone && t.ReminderSentAt == null && t.DueAt <= utcNow).Take(take)]);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

public class WorkTaskTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsTheFields_AndIsOpen()
    {
        var owner = Guid.NewGuid();
        var ticket = Guid.NewGuid();

        var task = WorkTask.Create(owner, "  Call Nour  ", "About the invoice", Now.AddHours(2), ticket, Now);

        Assert.Equal((owner, "Call Nour", "About the invoice", Now.AddHours(2), (Guid?)ticket), (task.OwnerId, task.Title, task.Description, task.DueAt, task.TicketId));
        Assert.False(task.IsDone);
        Assert.Null(task.ReminderSentAt);
    }

    [Fact]
    public void Create_WithADueTimeNotInTheFuture_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => WorkTask.Create(Guid.NewGuid(), "x", null, Now, null, Now));
        Assert.Throws<ArgumentException>(() => WorkTask.Create(Guid.NewGuid(), "x", null, Now.AddMinutes(-1), null, Now));
    }

    [Fact]
    public void Create_RejectsABlankTitle_AndANonUtcTime()
    {
        Assert.Throws<ArgumentException>(() => WorkTask.Create(Guid.NewGuid(), " ", null, Now.AddHours(1), null, Now));
        Assert.Throws<ArgumentException>(() => WorkTask.Create(Guid.NewGuid(), "x", null, DateTime.Now.AddHours(1), null, Now));
    }

    [Fact]
    public void MarkDone_AndMarkReminderSent_SetTheTimeOnce()
    {
        var task = WorkTask.Create(Guid.NewGuid(), "x", null, Now.AddHours(1), null, Now);

        Assert.True(task.MarkDone(Now.AddMinutes(5)));
        Assert.False(task.MarkDone(Now.AddMinutes(9)));
        Assert.True(task.IsDone);
        Assert.Equal(Now.AddMinutes(5), task.CompletedAt);
        Assert.True(task.MarkReminderSent(Now.AddHours(1)));
        Assert.False(task.MarkReminderSent(Now.AddHours(2)));
        Assert.Equal(Now.AddHours(1), task.ReminderSentAt);
    }
}

public class TaskServiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTaskRepository _repository = new();
    private readonly TestClock _clock = new(Now);

    private TaskService Service(Guid? user = null) =>
        new(_repository, new FakeCurrentUser(user ?? Me), _clock, new CreateTaskRequestValidator(), new ListTasksQueryValidator());

    private Task<TaskResponse> CreateAsync(string? title = "Call Nour", DateTime? dueAt = null, Guid? ticketId = null, Guid? user = null) =>
        Service(user).CreateAsync(new CreateTaskRequest(title, null, dueAt ?? Now.UtcDateTime.AddHours(1), ticketId), CancellationToken.None);

    [Fact]
    public async Task Create_ReturnsTheOpenTaskOfTheCurrentUser()
    {
        var task = await CreateAsync();

        Assert.Equal(("Call Nour", Now.UtcDateTime.AddHours(1), false), (task.Title, task.DueAt, task.IsDone));
        Assert.Equal(Me, Assert.Single(_repository.Tasks).OwnerId);
    }

    [Fact]
    public async Task Create_WithADueDateInThePast_IsAValidationErrorOnDueAt()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(dueAt: Now.UtcDateTime.AddMinutes(-5)));

        Assert.Contains("dueAt", error.Errors.Keys);
        Assert.Empty(_repository.Tasks);
    }

    [Fact]
    public async Task Create_WithoutATitleOrDueDate_IsAValidationError()
    {
        var blank = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(title: " "));
        Assert.Contains("title", blank.Errors.Keys);

        var noDue = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new CreateTaskRequest("x", null, null, null), CancellationToken.None));
        Assert.Contains("dueAt", noDue.Errors.Keys);
    }

    [Fact]
    public async Task Create_WithAnUnknownTicket_IsAValidationErrorOnTicketId()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(ticketId: Guid.NewGuid()));

        Assert.Contains("ticketId", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_LinkedToAKnownTicket_KeepsTheLink()
    {
        var ticket = Guid.NewGuid();
        _repository.KnownTickets.Add(ticket);

        Assert.Equal(ticket, (await CreateAsync(ticketId: ticket)).TicketId);
    }

    [Fact]
    public async Task List_ReturnsMyOpenTasksByDueTime_AndDoneRemovesOne()
    {
        var later = await CreateAsync("Later", Now.UtcDateTime.AddHours(5));
        var soon = await CreateAsync("Soon", Now.UtcDateTime.AddHours(1));
        await CreateAsync("Not mine", user: Guid.NewGuid());

        var open = await Service().ListAsync(new ListTasksQuery(null), CancellationToken.None);
        Assert.Equal([soon.Id, later.Id], open.Select(t => t.Id));

        await Service().MarkDoneAsync(soon.Id, CancellationToken.None);

        Assert.Equal([later.Id], (await Service().ListAsync(new ListTasksQuery(null), CancellationToken.None)).Select(t => t.Id)); // AC 4
        Assert.Equal([soon.Id], (await Service().ListAsync(new ListTasksQuery("done"), CancellationToken.None)).Select(t => t.Id));
    }

    [Fact]
    public async Task MarkDone_OfSomebodyElsesTask_IsNotFound()
    {
        var foreign = await CreateAsync(user: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Service().MarkDoneAsync(foreign.Id, CancellationToken.None));
    }

    [Fact]
    public async Task List_WithAnUnknownStatus_IsAValidationError() =>
        await Assert.ThrowsAsync<ValidationException>(() => Service().ListAsync(new ListTasksQuery("someday"), CancellationToken.None));
}

public class TaskReminderJobTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTaskRepository _repository = new();
    private readonly FakeNotificationDispatcher _notifications = new();
    private readonly TestClock _clock = new(Start);
    private readonly TaskReminderJob _job;

    public TaskReminderJobTests() => _job = new TaskReminderJob(_repository, _notifications, _clock);

    private WorkTask Add(Guid? ticket = null, int dueInMinutes = 60)
    {
        var task = WorkTask.Create(Guid.NewGuid(), "Call Nour", null, Start.UtcDateTime.AddMinutes(dueInMinutes), ticket, Start.UtcDateTime);
        _repository.Tasks.Add(task);
        return task;
    }

    [Fact]
    public async Task Run_AtTheDueTime_SendsOneReminderToTheOwner_WithTitleAndTicket()
    {
        var ticket = Guid.NewGuid();
        var task = Add(ticket);
        _clock.UtcNow = Start.AddMinutes(60);

        var sent = await _job.RunAsync(CancellationToken.None);

        Assert.Equal(1, sent);
        var request = Assert.Single(_notifications.Requests);
        Assert.Equal((NotificationType.TaskReminder, $"task-reminder:{task.Id}", "Call Nour", (Guid?)ticket), (request.Type, request.DedupKey, request.Text, request.TicketId));
        Assert.Equal([task.OwnerId], request.UserIds);
        Assert.NotNull(task.ReminderSentAt);
    }

    [Fact]
    public async Task Run_BeforeTheDueTime_SendsNothing()
    {
        Add();
        _clock.UtcNow = Start.AddMinutes(59);

        Assert.Equal(0, await _job.RunAsync(CancellationToken.None));
        Assert.Empty(_notifications.Requests);
    }

    [Fact]
    public async Task Run_ForADoneTask_SendsNothing()
    {
        var task = Add();
        task.MarkDone(Start.UtcDateTime.AddMinutes(10));
        _clock.UtcNow = Start.AddMinutes(90);

        Assert.Equal(0, await _job.RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Run_Twice_RemindsOnce()
    {
        Add();
        _clock.UtcNow = Start.AddMinutes(61);

        await _job.RunAsync(CancellationToken.None);
        var second = await _job.RunAsync(CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Single(_notifications.Requests);
    }
}
