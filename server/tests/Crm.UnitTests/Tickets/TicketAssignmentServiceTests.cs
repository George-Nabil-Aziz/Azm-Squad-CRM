using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketAssignmentServiceTests
{
    private static readonly Guid SupervisorId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid OtherAgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketHistoryRecorder _history = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Ticket _ticket;

    public TicketAssignmentServiceTests()
    {
        var customerId = _tickets.AddCustomer("Nour Trading");
        _ticket = Ticket.Create(customerId, "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, SupervisorId, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
        _tickets.Assignees.Add(new TicketAssigneeResponse(AgentId, "Sara Agent"));
        _tickets.Assignees.Add(new TicketAssigneeResponse(OtherAgentId, "Omar Agent"));
        _tickets.Assignees.Add(new TicketAssigneeResponse(SupervisorId, "Team Lead"));
    }

    private TicketAssignmentService ServiceFor(Guid userId, bool canAssign) =>
        new(_tickets, _history, new AssignmentUser(userId, canAssign), _clock);

    private Task<TicketResponse> AssignAsync(TicketAssignmentService service, Guid? assigneeId) =>
        service.AssignAsync(_ticket.Id, new AssignTicketRequest(assigneeId), CancellationToken.None);

    [Fact]
    public async Task Supervisor_Assigns_UpdatesTheAssignee_AndRecordsAHistoryEntry()
    {
        var response = await AssignAsync(ServiceFor(SupervisorId, canAssign: true), AgentId);

        Assert.Equal(AgentId, _ticket.AssigneeId);
        Assert.Equal(AgentId, response.AssigneeId);
        var entry = Assert.Single(_history.Entries);
        Assert.Equal((_ticket.Id, TicketHistoryField.Assignee, (string?)null, (string?)"Sara Agent", _clock.UtcNow.UtcDateTime),
            (entry.TicketId, entry.Field, entry.OldValue, entry.NewValue, entry.UtcNow));
        Assert.Equal(1, _tickets.SaveCount);
    }

    [Fact]
    public async Task Reassigning_RecordsTheOldAndNewName()
    {
        var supervisor = ServiceFor(SupervisorId, canAssign: true);
        await AssignAsync(supervisor, AgentId);

        await AssignAsync(supervisor, OtherAgentId);

        var entry = _history.Entries[^1];
        Assert.Equal(("Sara Agent", "Omar Agent"), (entry.OldValue, entry.NewValue));
    }

    [Fact]
    public async Task Unassigning_RecordsNoNewValue()
    {
        var supervisor = ServiceFor(SupervisorId, canAssign: true);
        await AssignAsync(supervisor, AgentId);

        var response = await AssignAsync(supervisor, null);

        Assert.Null(response.AssigneeId);
        var entry = _history.Entries[^1];
        Assert.Equal(("Sara Agent", (string?)null), (entry.OldValue, entry.NewValue));
    }

    [Fact]
    public async Task AssigningTheCurrentAssignee_ChangesNothing_AndRecordsNothing()
    {
        var supervisor = ServiceFor(SupervisorId, canAssign: true);
        await AssignAsync(supervisor, AgentId);
        var updatedAt = _ticket.UpdatedAt;
        _clock.UtcNow = _clock.UtcNow.AddHours(1);

        await AssignAsync(supervisor, AgentId);

        Assert.Single(_history.Entries);
        Assert.Equal(updatedAt, _ticket.UpdatedAt);
        Assert.Equal(1, _tickets.SaveCount);
    }

    [Fact]
    public async Task AgentWithoutAssignPermission_AssigningSomeoneElse_IsForbidden()
    {
        var agent = ServiceFor(AgentId, canAssign: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => AssignAsync(agent, OtherAgentId));

        Assert.Null(_ticket.AssigneeId);
        Assert.Empty(_history.Entries);
        Assert.Equal(0, _tickets.SaveCount);
    }

    [Fact]
    public async Task AgentWithoutAssignPermission_CanTakeTheTicket_AndReleaseIt()
    {
        var agent = ServiceFor(AgentId, canAssign: false);

        await AssignAsync(agent, AgentId);
        Assert.Equal(AgentId, _ticket.AssigneeId);
        await AssignAsync(agent, null);

        Assert.Null(_ticket.AssigneeId);
        Assert.Equal(2, _history.Entries.Count);
    }

    [Fact]
    public async Task AgentWithoutAssignPermission_CannotUnassignSomeoneElsesTicket()
    {
        await AssignAsync(ServiceFor(SupervisorId, canAssign: true), OtherAgentId);

        await Assert.ThrowsAsync<ForbiddenException>(() => AssignAsync(ServiceFor(AgentId, canAssign: false), null));

        Assert.Equal(OtherAgentId, _ticket.AssigneeId);
    }

    [Fact]
    public async Task AssigningAnInactiveOrUnknownUser_ThrowsValidationException_OnAssigneeId()
    {
        var supervisor = ServiceFor(SupervisorId, canAssign: true);

        var error = await Assert.ThrowsAsync<ValidationException>(() => AssignAsync(supervisor, Guid.NewGuid()));

        Assert.Contains("assigneeId", error.Errors.Keys);
        Assert.Null(_ticket.AssigneeId);
        Assert.Empty(_history.Entries);
    }

    [Fact]
    public async Task UnknownTicket_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(SupervisorId, true).AssignAsync(Guid.NewGuid(), new AssignTicketRequest(AgentId), CancellationToken.None));

    private sealed class AssignmentUser(Guid userId, bool canAssign) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsInRole(string role) => false;

        public bool HasPermission(string permission) => canAssign;
    }
}
