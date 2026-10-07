using Crm.Application.Ai;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.UnitTests.Settings;
using Crm.UnitTests.Sla;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Ai;

internal sealed class FakeAiClassificationService : IAiClassificationService
{
    public AiClassificationOutcome? Outcome { get; set; }

    public int ClassifyCalls { get; private set; }

    public List<(Guid TicketId, AiClassificationOutcome Outcome, bool CategoryApplied, bool PriorityApplied)> Saved { get; } = [];

    public List<(Guid TicketId, Guid? CategoryId)> CategoryChanges { get; } = [];

    public List<(Guid TicketId, TicketPriority Priority)> PriorityChanges { get; } = [];

    public Task<AiClassificationOutcome?> ClassifyAsync(string subject, string? description, CancellationToken cancellationToken)
    {
        ClassifyCalls++;
        return Task.FromResult(Outcome);
    }

    public void Save(Guid ticketId, AiClassificationOutcome outcome, bool categoryApplied, bool priorityApplied, DateTime utcNow) =>
        Saved.Add((ticketId, outcome, categoryApplied, priorityApplied));

    public Task RecordCategoryChangeAsync(Guid ticketId, Guid? newCategoryId, DateTime utcNow, CancellationToken cancellationToken)
    {
        CategoryChanges.Add((ticketId, newCategoryId));
        return Task.CompletedTask;
    }

    public Task RecordPriorityChangeAsync(Guid ticketId, TicketPriority newPriority, DateTime utcNow, CancellationToken cancellationToken)
    {
        PriorityChanges.Add((ticketId, newPriority));
        return Task.CompletedTask;
    }

    public Task<TicketAiClassificationResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>CRM-52 in the ticket use cases: the AI result is applied (or only stored) when a ticket is created, and overrides are recorded.</summary>
public class TicketServiceAiTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeAiClassificationService _ai = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeSlaPolicyRepository _sla;
    private readonly TicketService _service;
    private readonly Guid _customerId;
    private readonly TicketCategory _billing;
    private readonly TicketCategory _support;

    public TicketServiceAiTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _sla = new FakeSlaPolicyRepository(_clock.UtcNow.UtcDateTime);
        _service = new TicketService(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(AgentId), _clock,
            new CreateTicketRequestValidator(), new ListTicketsQueryValidator(), _sla, new FakeTicketHistoryRecorder(),
            new FakeSystemSettingsProvider(), aiClassification: _ai);
        _customerId = _tickets.AddCustomer("Nour Trading");
        _billing = TicketCategory.Create("Billing", _clock.UtcNow.UtcDateTime);
        _support = TicketCategory.Create("Support", _clock.UtcNow.UtcDateTime);
        _categories.Add(_billing);
        _categories.Add(_support);
    }

    private Task<TicketResponse> CreateAsync(Guid? categoryId = null, string? priority = null) =>
        _service.CreateAsync(new CreateTicketRequest(_customerId, "Invoice is wrong", "Line 3 is charged twice.", categoryId, priority), CancellationToken.None);

    [Fact]
    public async Task AboveTheThreshold_TheCategoryAndPriorityAreApplied_BeforeTheSlaTimers()
    {
        _ai.Outcome = new AiClassificationOutcome(_billing.Id, TicketPriority.High, 0.95, true);

        var ticket = await CreateAsync();

        Assert.Equal((_billing.Id, "high"), (ticket.CategoryId, ticket.Priority));
        var high = await _sla.FindAsync(TicketPriority.High, CancellationToken.None);
        Assert.Equal(_clock.UtcNow.UtcDateTime.AddMinutes(high!.ResponseMinutes), ticket.ResponseDueAt);
        var saved = Assert.Single(_ai.Saved);
        Assert.Equal((ticket.Id, true, true), (saved.TicketId, saved.CategoryApplied, saved.PriorityApplied));
    }

    [Fact]
    public async Task BelowTheThreshold_ItIsOnlyStored_AndTheTicketKeepsTheDefaults()
    {
        _ai.Outcome = new AiClassificationOutcome(_billing.Id, TicketPriority.High, 0.4, false);

        var ticket = await CreateAsync();

        Assert.Null(ticket.CategoryId);
        Assert.Equal("mid", ticket.Priority);
        var saved = Assert.Single(_ai.Saved);
        Assert.Equal((false, false), (saved.CategoryApplied, saved.PriorityApplied));
        Assert.Equal(0.4, saved.Outcome.Confidence);
    }

    [Fact]
    public async Task WhatTheCreatorChose_IsNeverReplaced_ButTheSuggestionIsStored()
    {
        _ai.Outcome = new AiClassificationOutcome(_billing.Id, TicketPriority.High, 0.95, true);

        var ticket = await CreateAsync(_support.Id, "low");

        Assert.Equal((_support.Id, "low"), (ticket.CategoryId, ticket.Priority));
        var saved = Assert.Single(_ai.Saved);
        Assert.Equal((false, false), (saved.CategoryApplied, saved.PriorityApplied));
    }

    [Fact]
    public async Task OnlyTheMissingPart_IsFilledByTheAi()
    {
        _ai.Outcome = new AiClassificationOutcome(_billing.Id, TicketPriority.High, 0.95, true);

        var ticket = await CreateAsync(categoryId: _support.Id, priority: null);

        Assert.Equal((_support.Id, "high"), (ticket.CategoryId, ticket.Priority));
        var saved = Assert.Single(_ai.Saved);
        Assert.Equal((false, true), (saved.CategoryApplied, saved.PriorityApplied));
    }

    [Fact]
    public async Task WhenAiIsUnavailable_TheTicketIsCreatedNormally()
    {
        _ai.Outcome = null;

        var ticket = await CreateAsync();

        Assert.Equal("TKT-000001", ticket.Number);
        Assert.Equal((null, "mid"), (ticket.CategoryId, ticket.Priority));
        Assert.Empty(_ai.Saved);
        Assert.Single(_tickets.Tickets);
    }

    [Fact]
    public async Task APriorityChange_IsReportedForOverrideTracking()
    {
        var ticket = await CreateAsync();

        await _service.ChangePriorityAsync(ticket.Id, new ChangeTicketPriorityRequest("low"), CancellationToken.None);

        Assert.Equal([(ticket.Id, TicketPriority.Low)], _ai.PriorityChanges);
    }

    [Fact]
    public async Task APriorityChange_ToTheSameValue_IsNotReported()
    {
        var ticket = await CreateAsync();

        await _service.ChangePriorityAsync(ticket.Id, new ChangeTicketPriorityRequest("mid"), CancellationToken.None);

        Assert.Empty(_ai.PriorityChanges);
    }

    [Fact]
    public async Task ACategoryChange_IsReportedForOverrideTracking()
    {
        var ticket = await CreateAsync(_billing.Id, "mid");
        var changer = new TicketCategoryChangeService(_tickets, _categories, new FakeTicketHistoryRecorder(), _clock, _ai);

        await changer.ChangeAsync(ticket.Id, new ChangeTicketCategoryRequest(_support.Id), CancellationToken.None);
        await changer.ChangeAsync(ticket.Id, new ChangeTicketCategoryRequest(_support.Id), CancellationToken.None); // same value: nothing

        Assert.Equal([(ticket.Id, (Guid?)_support.Id)], _ai.CategoryChanges);
    }
}
