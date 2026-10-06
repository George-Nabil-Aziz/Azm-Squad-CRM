using Crm.Application.Sla;
using Crm.Domain.Notifications;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>CRM-21 / CRM-22: the SLA monitor job, with an in-memory repository and a controllable clock.</summary>
public class SlaMonitorJobTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTicketSlaRepository _repository = new();
    private readonly FakeSlaNotifier _notifier = new();
    private readonly TestClock _clock = new(Start);
    private readonly SlaMonitorJob _job;

    public SlaMonitorJobTests() => _job = new SlaMonitorJob(_repository, _notifier, _clock);

    /// <summary>High ticket: response due after 100 min (warning at 80), resolution after 400 min.</summary>
    private Ticket AddTicket(Guid? assigneeId = null)
    {
        var created = Start.UtcDateTime;
        var ticket = Ticket.Create(Guid.NewGuid(), "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, null, created);
        ticket.ApplySla(SlaPolicy.Create(TicketPriority.High, 100, 400, created));
        if (assigneeId is { } id)
        {
            ticket.GetType().GetProperty(nameof(Ticket.AssigneeId))!.SetValue(ticket, id);
        }

        _repository.Tickets.Add(ticket);
        return ticket;
    }

    private IEnumerable<SlaEventType> EventTypes(Guid ticketId) =>
        _repository.Events.Where(e => e.TicketId == ticketId).Select(e => e.Type);

    // CRM-21

    [Fact]
    public async Task Run_MarksBreachesAndAddsOneEventPerKind()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(500);

        var result = await _job.RunAsync(CancellationToken.None);

        Assert.True(ticket.ResponseBreached);
        Assert.True(ticket.ResolutionBreached);
        Assert.Equal(1, result.ResponseBreaches);
        Assert.Equal(1, result.ResolutionBreaches);
        Assert.Single(_repository.Events, e => e.Type == SlaEventType.ResponseBreached);
        Assert.Single(_repository.Events, e => e.Type == SlaEventType.ResolutionBreached);
        Assert.Equal(ticket.ResponseDueAt, _repository.Events.Single(e => e.Type == SlaEventType.ResponseBreached).DueAt);
    }

    [Fact]
    public async Task Run_Twice_AddsNoDuplicateEvents()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(110);

        await _job.RunAsync(CancellationToken.None);
        var eventsAfterFirst = _repository.Events.Count;
        var second = await _job.RunAsync(CancellationToken.None);

        Assert.Equal(eventsAfterFirst, _repository.Events.Count);
        Assert.Single(_repository.Events, e => e.Type == SlaEventType.ResponseBreached);
        Assert.Equal(new SlaMonitorResult(0, 0), second);
        Assert.Equal(1, ticket.EscalationLevel);
    }

    [Fact]
    public async Task Run_UsesTheClock()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(99);
        await _job.RunAsync(CancellationToken.None);
        Assert.False(ticket.ResponseBreached);

        _clock.UtcNow = Start.AddMinutes(100);
        await _job.RunAsync(CancellationToken.None);

        Assert.True(ticket.ResponseBreached);
        Assert.Equal(_clock.UtcNow.UtcDateTime, _repository.Events.Single(e => e.Type == SlaEventType.ResponseBreached).OccurredAt);
    }

    [Fact]
    public async Task Run_TicketRepliedInTime_IsNotBreached()
    {
        var ticket = AddTicket();
        ticket.MarkFirstResponse(Start.UtcDateTime.AddMinutes(10));
        _clock.UtcNow = Start.AddMinutes(100);

        await _job.RunAsync(CancellationToken.None);

        Assert.False(ticket.ResponseBreached);
        Assert.Empty(_repository.Events);
    }

    // CRM-22

    [Fact]
    public async Task Run_At80Percent_WarnsTheAssignee_Once()
    {
        var assignee = Guid.NewGuid();
        var ticket = AddTicket(assignee);
        _clock.UtcNow = Start.AddMinutes(80);

        var first = await _job.RunAsync(CancellationToken.None);
        var second = await _job.RunAsync(CancellationToken.None);

        Assert.Equal(1, first.Warnings);
        Assert.Equal(0, second.Warnings);
        Assert.Equal([SlaEventType.ResponseWarning], EventTypes(ticket.Id));
        var notification = Assert.Single(_repository.Notifications);
        Assert.Equal(assignee, notification.RecipientUserId);
        Assert.Null(notification.RecipientRole);
        Assert.Equal(NotificationType.SlaWarning, notification.Type);
        Assert.Single(_notifier.Notices, n => n.Type == NotificationType.SlaWarning && n.RecipientUserId == assignee);
    }

    [Fact]
    public async Task Run_BeforeTheWarningTime_DoesNothing()
    {
        AddTicket(Guid.NewGuid());
        _clock.UtcNow = Start.AddMinutes(79);

        await _job.RunAsync(CancellationToken.None);

        Assert.Empty(_repository.Events);
        Assert.Empty(_repository.Notifications);
    }

    [Fact]
    public async Task Run_Unassigned_WarnsSupervisors()
    {
        AddTicket();
        _clock.UtcNow = Start.AddMinutes(85);

        await _job.RunAsync(CancellationToken.None);

        var notification = Assert.Single(_repository.Notifications);
        Assert.Null(notification.RecipientUserId);
        Assert.Equal(Notification.SupervisorRole, notification.RecipientRole);
    }

    [Fact]
    public async Task Run_OnBreach_EscalatesToSupervisor_AndAddsAHistoryEvent()
    {
        var ticket = AddTicket(Guid.NewGuid());
        _clock.UtcNow = Start.AddMinutes(101);

        var result = await _job.RunAsync(CancellationToken.None);

        Assert.Equal(1, ticket.EscalationLevel);
        Assert.Equal(1, result.Escalations);
        var history = Assert.Single(_repository.Events, e => e.Type == SlaEventType.Escalated);
        Assert.Equal(1, history.Level);
        var notification = Assert.Single(_repository.Notifications, n => n.Type == NotificationType.SlaEscalation);
        Assert.Equal(Notification.SupervisorRole, notification.RecipientRole);
        Assert.Equal(1, notification.Level);
        Assert.Single(_notifier.Notices, n => n.Type == NotificationType.SlaEscalation && n.Level == 1);
    }

    [Fact]
    public async Task Run_BreachedTicket_IsNotWarnedAnymore()
    {
        var ticket = AddTicket(Guid.NewGuid());
        _clock.UtcNow = Start.AddMinutes(101);

        await _job.RunAsync(CancellationToken.None);

        Assert.DoesNotContain(SlaEventType.ResponseWarning, EventTypes(ticket.Id));
    }

    [Fact]
    public async Task Run_Twice_NeverEscalatesTheSameLevelAgain()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(101);

        await _job.RunAsync(CancellationToken.None);
        await _job.RunAsync(CancellationToken.None);
        _clock.UtcNow = Start.AddMinutes(150);
        await _job.RunAsync(CancellationToken.None);

        Assert.Equal(1, ticket.EscalationLevel);
        Assert.Single(_repository.Events, e => e.Type == SlaEventType.Escalated);
    }

    [Fact]
    public async Task Run_ResponseThenResolutionBreach_Levels1And2()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(101);
        await _job.RunAsync(CancellationToken.None);

        _clock.UtcNow = Start.AddMinutes(401);
        await _job.RunAsync(CancellationToken.None);

        Assert.Equal(2, ticket.EscalationLevel);
        Assert.Equal([1, 2], _repository.Events.Where(e => e.Type == SlaEventType.Escalated).Select(e => e.Level).Order());
    }

    [Fact]
    public async Task Run_ResolvedTicket_NoWarningNoEscalation()
    {
        var ticket = AddTicket(Guid.NewGuid());
        ticket.MarkFirstResponse(Start.UtcDateTime.AddMinutes(200)); // late reply, breached
        ticket.MarkResolved(Start.UtcDateTime.AddMinutes(500)); // late resolution, breached
        _clock.UtcNow = Start.AddMinutes(600);

        await _job.RunAsync(CancellationToken.None);

        Assert.True(ticket.ResponseBreached);
        Assert.True(ticket.ResolutionBreached);
        Assert.Equal(0, ticket.EscalationLevel);
        Assert.DoesNotContain(SlaEventType.Escalated, EventTypes(ticket.Id));
        Assert.DoesNotContain(SlaEventType.ResponseWarning, EventTypes(ticket.Id));
        Assert.Empty(_repository.Notifications);
    }

    [Fact]
    public async Task Run_NotifierFailure_DoesNotStopTheJob()
    {
        var first = AddTicket();
        var second = AddTicket();
        _notifier.Throws = true;
        _clock.UtcNow = Start.AddMinutes(101);

        var result = await _job.RunAsync(CancellationToken.None);

        Assert.Equal(2, result.Escalations);
        Assert.Equal(1, first.EscalationLevel);
        Assert.Equal(1, second.EscalationLevel);
        Assert.Equal(2, _repository.Notifications.Count);
    }
}

/// <summary>In-memory SLA storage with the same candidate rules as the EF Core repository.</summary>
internal sealed class FakeTicketSlaRepository : ITicketSlaRepository
{
    public List<Ticket> Tickets { get; } = [];

    public List<TicketSlaEvent> Events { get; } = [];

    public List<Notification> Notifications { get; } = [];

    public Task<IReadOnlyList<Ticket>> ListBreachCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Ticket>>([.. Tickets
            .Where(t => (!t.ResponseBreached && t.IsResponseBreachedAt(utcNow)) || (!t.ResolutionBreached && t.IsResolutionBreachedAt(utcNow)))
            .Take(take)]);

    public Task<IReadOnlyList<Ticket>> ListWarningCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Ticket>>([.. Tickets
            .Where(t => t.ResponseWarningAt <= utcNow && t.ResponseWarnedAt == null && t.FirstResponseAt == null && t.ResolvedAt == null)
            .Take(take)]);

    public void AddEvent(TicketSlaEvent slaEvent) => Events.Add(slaEvent);

    public void AddNotification(Notification notification) => Notifications.Add(notification);

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}

/// <summary>Collects the notices; can be told to fail like a broken mail server.</summary>
internal sealed class FakeSlaNotifier : ISlaNotifier
{
    public List<SlaNotice> Notices { get; } = [];

    public bool Throws { get; set; }

    public Task NotifyAsync(SlaNotice notice, CancellationToken cancellationToken)
    {
        Notices.Add(notice);
        return Throws ? throw new InvalidOperationException("The notifier is down.") : Task.CompletedTask;
    }
}
