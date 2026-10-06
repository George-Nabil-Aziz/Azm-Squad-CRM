using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

internal sealed class FakeAssignmentRepository : IAssignmentRepository
{
    public bool Enabled { get; set; }

    public bool Throws { get; set; }

    public List<AssignmentCandidate> Candidates { get; } = [];

    public List<AssignmentAgentResponse> Agents { get; } = [];

    public Task<bool> IsAutoAssignEnabledAsync(CancellationToken cancellationToken) =>
        Throws ? throw new InvalidOperationException("db down") : Task.FromResult(Enabled);

    public Task SetAutoAssignEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        Enabled = enabled;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AssignmentCandidate>> ListCandidatesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssignmentCandidate>>(Candidates);

    public Task<IReadOnlyList<AssignmentAgentResponse>> ListAgentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssignmentAgentResponse>>(Agents);

    public Task<bool> SetOnDutyAsync(Guid userId, bool onDuty, CancellationToken cancellationToken)
    {
        var index = Agents.FindIndex(a => a.Id == userId);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Agents[index] = Agents[index] with { OnDuty = onDuty };
        return Task.FromResult(true);
    }
}

public class AutoAssignmentServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeAssignmentRepository _repository = new();
    private readonly FakeTicketHistoryRecorder _history = new();
    private readonly AutoAssignmentService _service;

    public AutoAssignmentServiceTests() => _service = new AutoAssignmentService(_repository, _history);

    private static Ticket NewTicket() =>
        Ticket.Create(Guid.NewGuid(), "Printer", null, null, TicketPriority.Mid, TicketChannel.Manual, null, Now);

    [Fact]
    public async Task On_AssignsLeastLoadedAgent_AndRecordsSystemHistory()
    {
        var sara = new AssignmentCandidate(Guid.NewGuid(), "Sara Agent", 1);
        _repository.Enabled = true;
        _repository.Candidates.AddRange([new AssignmentCandidate(Guid.NewGuid(), "Omar Agent", 4), sara]);
        var ticket = NewTicket();

        var assigned = await _service.TryAssignAsync(ticket, Now, CancellationToken.None);

        Assert.Equal(sara.Id, assigned);
        Assert.Equal(sara.Id, ticket.AssigneeId);
        var entry = Assert.Single(_history.Entries);
        Assert.Equal((ticket.Id, TicketHistoryField.Assignee, (string?)null, (string?)"Sara Agent", true),
            (entry.TicketId, entry.Field, entry.OldValue, entry.NewValue, entry.System));
    }

    [Fact]
    public async Task Off_LeavesTheTicketUnassigned()
    {
        _repository.Candidates.Add(new AssignmentCandidate(Guid.NewGuid(), "Sara Agent", 0));
        var ticket = NewTicket();

        Assert.Null(await _service.TryAssignAsync(ticket, Now, CancellationToken.None));
        Assert.Null(ticket.AssigneeId);
        Assert.Empty(_history.Entries);
    }

    [Fact]
    public async Task NoCandidates_LeavesUnassigned()
    {
        _repository.Enabled = true;
        var ticket = NewTicket();

        Assert.Null(await _service.TryAssignAsync(ticket, Now, CancellationToken.None));
        Assert.Null(ticket.AssigneeId);
    }

    [Fact]
    public async Task RepositoryFailure_IsSwallowed()
    {
        _repository.Throws = true;
        var ticket = NewTicket();

        Assert.Null(await _service.TryAssignAsync(ticket, Now, CancellationToken.None));
        Assert.Null(ticket.AssigneeId);
    }
}

public class AssignmentSettingsServiceTests
{
    private readonly FakeAssignmentRepository _repository = new();
    private readonly AssignmentSettingsService _service;

    public AssignmentSettingsServiceTests() => _service = new AssignmentSettingsService(_repository);

    [Fact]
    public async Task Update_TurnsAutoAssignOn()
    {
        var response = await _service.UpdateAsync(new UpdateAssignmentSettingsRequest(true), CancellationToken.None);

        Assert.True(response.AutoAssignEnabled);
        Assert.True(_repository.Enabled);
    }

    [Fact]
    public async Task Update_WithoutTheFlag_IsAValidationError() =>
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(new UpdateAssignmentSettingsRequest(null), CancellationToken.None));

    [Fact]
    public async Task SetOnDuty_UnknownAgent_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SetOnDutyAsync(Guid.NewGuid(), new SetAgentDutyRequest(false), CancellationToken.None));

    [Fact]
    public async Task SetOnDuty_UpdatesTheAgent()
    {
        var id = Guid.NewGuid();
        _repository.Agents.Add(new AssignmentAgentResponse(id, "Sara", true, 0));

        var response = await _service.SetOnDutyAsync(id, new SetAgentDutyRequest(false), CancellationToken.None);

        Assert.False(Assert.Single(response.Agents).OnDuty);
    }
}

public class TicketServiceAutoAssignTests
{
    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeAssignmentRepository _assignment = new();
    private readonly FakeTicketHistoryRecorder _history = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketService _service;
    private readonly Guid _customerId;

    public TicketServiceAutoAssignTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketService(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(Guid.NewGuid()),
            _clock, new CreateTicketRequestValidator(), new ListTicketsQueryValidator(),
            new Crm.UnitTests.Sla.FakeSlaPolicyRepository(_clock.UtcNow.UtcDateTime), _history,
            new Crm.UnitTests.Settings.FakeSystemSettingsProvider(), new AutoAssignmentService(_assignment, _history));
        _customerId = _tickets.AddCustomer("Nour Trading");
    }

    [Fact]
    public async Task Create_WithAutoAssignOn_ReturnsTheAssignedTicket_AndSavesHistoryOnce()
    {
        var sara = new AssignmentCandidate(Guid.NewGuid(), "Sara Agent", 0);
        _assignment.Enabled = true;
        _assignment.Candidates.Add(sara);
        _tickets.Assignees.Add(new TicketAssigneeResponse(sara.Id, sara.Name));

        var created = await _service.CreateAsync(new CreateTicketRequest(_customerId, "Printer", null, null, "mid"), CancellationToken.None);

        Assert.Equal(sara.Id, created.AssigneeId);
        Assert.Equal("Sara Agent", created.AssigneeName);
        Assert.Single(_history.Entries, e => e.System && e.Field == TicketHistoryField.Assignee);
        Assert.Equal(1, _tickets.SaveCount);
    }

    [Fact]
    public async Task Create_WithAutoAssignOff_StaysUnassigned()
    {
        var created = await _service.CreateAsync(new CreateTicketRequest(_customerId, "Printer", null, null, "mid"), CancellationToken.None);

        Assert.Null(created.AssigneeId);
        Assert.Empty(_history.Entries);
    }
}
