using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Notifications;
using Crm.Application.Sla;
using Crm.Application.Tickets;
using Crm.Domain.Notifications;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Notifications;

/// <summary>In-memory notification storage with the contract of the EF Core repository (unique user + key).</summary>
internal sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _pending = [];

    public List<Notification> Stored { get; } = [];

    public Dictionary<Guid, string> TicketNumbers { get; } = [];

    public void Add(Notification notification) => _pending.Add(notification);

    public Task<bool> ExistsAsync(Guid userId, string dedupKey, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.Any(n => n.RecipientUserId == userId && n.DedupKey == dedupKey));

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        Stored.AddRange(_pending);
        _pending.Clear();
        return Task.FromResult(true);
    }

    public NotificationResponse ToResponse(Notification n) => new(
        n.Id, NotificationTypes.Name(n.Type), n.TicketId,
        n.TicketId is { } ticket && TicketNumbers.TryGetValue(ticket, out var number) ? number : null,
        n.Level, n.Text, n.CreatedAt, n.ReadAt);

    public Task<IReadOnlyList<NotificationResponse>> GetResponsesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NotificationResponse>>([.. Stored.Where(n => ids.Contains(n.Id)).Select(ToResponse)]);

    public Task<PagedResult<NotificationResponse>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = Stored.Where(n => n.RecipientUserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt).Select(ToResponse).ToList();
        return Task.FromResult(new PagedResult<NotificationResponse>([.. all.Skip((page - 1) * pageSize).Take(pageSize)], page, pageSize, all.Count));
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.Count(n => n.RecipientUserId == userId && !n.IsRead));

    public Task<Notification?> FindAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.FirstOrDefault(n => n.Id == id && n.RecipientUserId == userId));

    public Task SaveReadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = Stored.Where(n => n.RecipientUserId == userId && !n.IsRead).ToList();
        unread.ForEach(n => n.MarkRead(utcNow));
        return Task.FromResult(unread.Count);
    }
}

internal sealed class FakeStaffDirectory : IStaffDirectory
{
    public List<StaffContact> Active { get; } = [];

    public Dictionary<string, List<Guid>> Roles { get; } = [];

    public Task<IReadOnlyList<Guid>> ListActiveUserIdsInRoleAsync(string role, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Roles.TryGetValue(role, out var ids) ? [.. ids.Where(id => Active.Any(a => a.Id == id))] : []);

    public Task<IReadOnlyList<StaffContact>> ListActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffContact>>([.. Active.Where(a => ids.Contains(a.Id))]);
}

internal sealed class FakeNotificationPublisher : INotificationPublisher
{
    public List<(Guid UserId, NotificationResponse Notification, int Unread)> Published { get; } = [];

    public bool Throws { get; set; }

    public Task PublishAsync(Guid userId, NotificationResponse notification, int unreadCount, CancellationToken cancellationToken)
    {
        Published.Add((userId, notification, unreadCount));
        return Throws ? throw new InvalidOperationException("hub down") : Task.CompletedTask;
    }
}

internal sealed class FakeNotificationEmailSender : INotificationEmailSender
{
    public List<NotificationEmail> Sent { get; } = [];

    public bool Throws { get; set; }

    public Task SendAsync(NotificationEmail email, CancellationToken cancellationToken)
    {
        Sent.Add(email);
        return Throws ? throw new InvalidOperationException("smtp down") : Task.CompletedTask;
    }
}

/// <summary>Records the requests of services that notify (assignment, reminders, mentions).</summary>
internal sealed class FakeNotificationDispatcher : INotificationDispatcher
{
    public List<NotificationRequest> Requests { get; } = [];

    public Task<int> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(request.UserIds.Count);
    }
}

public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ForUser_SetsTheFields_AndIsUnread()
    {
        var user = Guid.NewGuid();
        var ticket = Guid.NewGuid();

        var notification = Notification.ForUser(user, ticket, NotificationType.Assignment, 0, "key", "  hello ", Now);

        Assert.Equal((user, (Guid?)ticket, NotificationType.Assignment, "key", "hello", Now), (notification.RecipientUserId, notification.TicketId, notification.Type, notification.DedupKey, notification.Text, notification.CreatedAt));
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void MarkRead_SetsTheTimeOnce()
    {
        var notification = Notification.ForUser(Guid.NewGuid(), null, NotificationType.Mention, 0, "k", null, Now);

        Assert.True(notification.MarkRead(Now.AddMinutes(1)));
        Assert.False(notification.MarkRead(Now.AddMinutes(5)));
        Assert.Equal(Now.AddMinutes(1), notification.ReadAt);
    }

    [Fact]
    public void ForUser_RejectsNonUtcTime_BlankKey_AndEmptyUser()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForUser(Guid.NewGuid(), null, NotificationType.Mention, 0, "k", null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => Notification.ForUser(Guid.NewGuid(), null, NotificationType.Mention, 0, " ", null, Now));
        Assert.Throws<ArgumentException>(() => Notification.ForUser(Guid.Empty, null, NotificationType.Mention, 0, "k", null, Now));
    }
}

public class NotificationDispatcherTests
{
    private static readonly Guid TicketId = Guid.NewGuid();

    private readonly FakeNotificationRepository _repository = new();
    private readonly FakeStaffDirectory _staff = new();
    private readonly FakeNotificationPublisher _publisher = new();
    private readonly FakeNotificationEmailSender _email = new();
    private readonly NotificationDispatcher _dispatcher;

    public NotificationDispatcherTests()
    {
        _repository.TicketNumbers[TicketId] = "TKT-000007";
        _dispatcher = new NotificationDispatcher(_repository, _staff, _publisher, _email,
            new TestClock(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
    }

    private StaffContact Staff(string name, string? email = null)
    {
        var contact = new StaffContact(Guid.NewGuid(), name, email ?? $"{name}@crm.local");
        _staff.Active.Add(contact);
        return contact;
    }

    private static NotificationRequest Request(NotificationType type, string key, params Guid[] users) =>
        new(type, key, users, TicketId: TicketId);

    [Fact]
    public async Task Notify_StoresOneRowPerRecipient_PushesIt_WithTheUnreadCount()
    {
        var sara = Staff("Sara");

        var created = await _dispatcher.NotifyAsync(Request(NotificationType.Assignment, "a1", sara.Id), CancellationToken.None);

        Assert.Equal(1, created);
        var stored = Assert.Single(_repository.Stored);
        Assert.Equal(sara.Id, stored.RecipientUserId);
        var pushed = Assert.Single(_publisher.Published);
        Assert.Equal((sara.Id, "assignment", "TKT-000007", 1), (pushed.UserId, pushed.Notification.Type, pushed.Notification.TicketNumber, pushed.Unread));
    }

    [Fact]
    public async Task Notify_Role_ExpandsToTheActiveUsersOfTheRole()
    {
        var lead = Staff("Lead");
        var other = Staff("Other");
        _staff.Roles["Supervisor"] = [lead.Id, other.Id, Guid.NewGuid()]; // the last one is inactive (not in Active)

        await _dispatcher.NotifyAsync(new NotificationRequest(NotificationType.SlaEscalation, "e1:1", [], "Supervisor", TicketId, 1), CancellationToken.None);

        Assert.Equal(new[] { lead.Id, other.Id }.Order(), _repository.Stored.Select(n => n.RecipientUserId!.Value).Order());
    }

    [Fact]
    public async Task Notify_InactiveUser_GetsNothing()
    {
        var created = await _dispatcher.NotifyAsync(Request(NotificationType.Mention, "m1", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, created);
        Assert.Empty(_repository.Stored);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Notify_TheSameEventTwice_NotifiesOnce()
    {
        var sara = Staff("Sara");
        var request = Request(NotificationType.SlaWarning, "sla-warning:1", sara.Id);

        await _dispatcher.NotifyAsync(request, CancellationToken.None);
        var second = await _dispatcher.NotifyAsync(request, CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Single(_repository.Stored);
        Assert.Single(_publisher.Published);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task Notify_AUserInTheListAndTheRole_GetsOneNotification()
    {
        var lead = Staff("Lead");
        _staff.Roles["Supervisor"] = [lead.Id];

        await _dispatcher.NotifyAsync(new NotificationRequest(NotificationType.SlaWarning, "w", [lead.Id], "Supervisor", TicketId), CancellationToken.None);

        Assert.Single(_repository.Stored);
    }

    [Fact]
    public async Task Sla_Notifications_SendAnEmail_Assignments_DoNot()
    {
        var sara = Staff("Sara", "sara@crm.local");

        await _dispatcher.NotifyAsync(Request(NotificationType.Assignment, "a", sara.Id), CancellationToken.None);
        Assert.Empty(_email.Sent);

        await _dispatcher.NotifyAsync(Request(NotificationType.SlaWarning, "w", sara.Id), CancellationToken.None);
        await _dispatcher.NotifyAsync(new NotificationRequest(NotificationType.SlaEscalation, "e", [sara.Id], TicketId: TicketId, Level: 2), CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.All(_email.Sent, e => Assert.Equal("sara@crm.local", e.ToAddress));
        Assert.Contains("TKT-000007", _email.Sent[0].Subject);
        Assert.Contains("level 2", _email.Sent[1].Subject);
    }

    [Fact]
    public async Task PublisherOrEmailFailure_DoesNotFail_AndTheRowStays()
    {
        var sara = Staff("Sara");
        _publisher.Throws = true;
        _email.Throws = true;

        var created = await _dispatcher.NotifyAsync(Request(NotificationType.SlaWarning, "w", sara.Id), CancellationToken.None);

        Assert.Equal(1, created);
        Assert.Single(_repository.Stored);
    }

    [Fact]
    public async Task UserWithoutEmail_IsNotMailed()
    {
        var contact = new StaffContact(Guid.NewGuid(), "No Mail", null);
        _staff.Active.Add(contact);

        await _dispatcher.NotifyAsync(Request(NotificationType.SlaWarning, "w", contact.Id), CancellationToken.None);

        Assert.Single(_repository.Stored);
        Assert.Empty(_email.Sent);
    }
}

public class SlaNotifierTests
{
    [Fact]
    public async Task Warning_ForTheAssignee_UsesOneKeyPerTicket()
    {
        var dispatcher = new FakeNotificationDispatcher();
        var ticket = Guid.NewGuid();
        var assignee = Guid.NewGuid();

        await new SlaNotifier(dispatcher).NotifyAsync(new SlaNotice(ticket, NotificationType.SlaWarning, 0, assignee, null), CancellationToken.None);

        var request = Assert.Single(dispatcher.Requests);
        Assert.Equal(($"sla-warning:{ticket}", NotificationType.SlaWarning, ticket), (request.DedupKey, request.Type, request.TicketId));
        Assert.Equal([assignee], request.UserIds);
        Assert.Null(request.Role);
    }

    [Fact]
    public async Task Escalation_GoesToTheRole_WithTheLevelInTheKey()
    {
        var dispatcher = new FakeNotificationDispatcher();
        var ticket = Guid.NewGuid();

        await new SlaNotifier(dispatcher).NotifyAsync(new SlaNotice(ticket, NotificationType.SlaEscalation, 2, null, "Supervisor"), CancellationToken.None);

        var request = Assert.Single(dispatcher.Requests);
        Assert.Equal(($"sla-escalation:{ticket}:2", "Supervisor", 2), (request.DedupKey, request.Role, request.Level));
        Assert.Empty(request.UserIds);
    }
}

public class NotificationServiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeNotificationRepository _repository = new();
    private readonly NotificationService _service;

    public NotificationServiceTests() =>
        _service = new NotificationService(_repository, new FakeCurrentUser(Me),
            new TestClock(new DateTimeOffset(Start.AddHours(1))), new ListNotificationsQueryValidator());

    private Notification Add(Guid user, int minute = 0)
    {
        var notification = Notification.ForUser(user, null, NotificationType.Mention, 0, Guid.NewGuid().ToString(), null, Start.AddMinutes(minute));
        _repository.Add(notification);
        _repository.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
        return notification;
    }

    [Fact]
    public async Task List_ReturnsOnlyMyNotifications_NewestFirst_AndCanFilterUnread()
    {
        var old = Add(Me, 1);
        var recent = Add(Me, 2);
        Add(Guid.NewGuid(), 3);
        old.MarkRead(Start.AddMinutes(10));

        var all = await _service.ListAsync(new ListNotificationsQuery(null, null, null), CancellationToken.None);
        var unread = await _service.ListAsync(new ListNotificationsQuery(true, null, null), CancellationToken.None);

        Assert.Equal([recent.Id, old.Id], all.Items.Select(n => n.Id));
        Assert.Equal([recent.Id], unread.Items.Select(n => n.Id));
    }

    [Fact]
    public async Task UnreadCount_CountsMyUnreadOnly()
    {
        Add(Me);
        Add(Me).MarkRead(Start);
        Add(Guid.NewGuid());

        Assert.Equal(1, (await _service.UnreadCountAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task MarkRead_SetsReadAt_AndLowersTheCount()
    {
        var notification = Add(Me);

        var response = await _service.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.NotNull(response.ReadAt);
        Assert.Equal(0, (await _service.UnreadCountAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task MarkRead_OfSomebodyElsesNotification_IsNotFound()
    {
        var foreign = Add(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(foreign.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task MarkAllRead_ReadsMineOnly()
    {
        Add(Me);
        Add(Me);
        var foreign = Add(Guid.NewGuid());

        var result = await _service.MarkAllReadAsync(CancellationToken.None);

        Assert.Equal(0, result.Count);
        Assert.Equal(0, (await _service.UnreadCountAsync(CancellationToken.None)).Count);
        Assert.False(foreign.IsRead);
    }

    [Fact]
    public async Task List_WithAPageSizeOverTheMaximum_IsAValidationError() =>
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new ListNotificationsQuery(null, 1, 500), CancellationToken.None));
}

public class AssignmentNotificationTests
{
    private static readonly Guid SupervisorId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeNotificationDispatcher _notifications = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Ticket _ticket;

    public AssignmentNotificationTests()
    {
        _ticket = Ticket.Create(_tickets.AddCustomer("Nour"), "Invoice", null, null, TicketPriority.High, TicketChannel.Manual, SupervisorId, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
        _tickets.Assignees.Add(new TicketAssigneeResponse(AgentId, "Sara Agent"));
        _tickets.Assignees.Add(new TicketAssigneeResponse(SupervisorId, "Team Lead"));
    }

    private Task AssignAsync(Guid actor, Guid? assignee) =>
        new TicketAssignmentService(_tickets, new FakeTicketHistoryRecorder(), new FakeCurrentUser(actor), _clock, _notifications)
            .AssignAsync(_ticket.Id, new AssignTicketRequest(assignee), CancellationToken.None);

    [Fact]
    public async Task AssigningToAnotherUser_NotifiesTheAssignee()
    {
        await AssignAsync(SupervisorId, AgentId);

        var request = Assert.Single(_notifications.Requests);
        Assert.Equal((NotificationType.Assignment, _ticket.Id), (request.Type, request.TicketId));
        Assert.Equal([AgentId], request.UserIds);
    }

    [Fact]
    public async Task AssigningToYourself_NotifiesNobody()
    {
        await AssignAsync(SupervisorId, SupervisorId);

        Assert.Empty(_notifications.Requests);
    }

    [Fact]
    public async Task AutoAssignment_NotifiesTheAgent()
    {
        var service = new AutoAssignmentService(new FakeAssignmentRepository(), new FakeTicketHistoryRecorder(), _notifications);

        await service.NotifyAssignedAsync(_ticket.Id, AgentId, _clock.UtcNow.UtcDateTime, CancellationToken.None);

        var request = Assert.Single(_notifications.Requests);
        Assert.Equal((NotificationType.Assignment, (Guid?)_ticket.Id), (request.Type, request.TicketId));
        Assert.Equal([AgentId], request.UserIds);
    }

    [Fact]
    public async Task Unassigning_NotifiesNobody()
    {
        await AssignAsync(SupervisorId, AgentId);
        _notifications.Requests.Clear();

        await AssignAsync(SupervisorId, null);

        Assert.Empty(_notifications.Requests);
    }
}
