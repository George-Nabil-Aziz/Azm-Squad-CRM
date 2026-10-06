using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>CRM-20: TicketService copies the SLA policy onto tickets and recalculates on a priority change.</summary>
public class TicketServiceSlaTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeSlaPolicyRepository _policies = new(Start.UtcDateTime.AddDays(-1));
    private readonly TestClock _clock = new(Start);
    private readonly TicketService _service;
    private readonly Guid _customerId;

    public TicketServiceSlaTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketService(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(Guid.NewGuid()),
            _clock, new CreateTicketRequestValidator(), new ListTicketsQueryValidator(), _policies);
        _customerId = _tickets.AddCustomer("Nour Trading");
    }

    private Task<TicketResponse> CreateAsync(string priority) =>
        _service.CreateAsync(new CreateTicketRequest(_customerId, "Invoice is wrong", null, null, priority), CancellationToken.None);

    private void SetPolicy(TicketPriority priority, int response, int resolution) =>
        _policies.Policies.Single(p => p.Priority == priority).Update(response, resolution, _clock.UtcNow.UtcDateTime);

    [Fact]
    public async Task Create_SetsDueTimesFromThePolicyOfThePriority()
    {
        SetPolicy(TicketPriority.High, 60, 240);

        var response = await CreateAsync("high");

        Assert.Equal(Start.UtcDateTime.AddHours(1), response.ResponseDueAt);
        Assert.Equal(Start.UtcDateTime.AddHours(4), response.ResolutionDueAt);
        Assert.Null(response.FirstResponseAt);
        Assert.Null(response.ResolvedAt);
        Assert.Equal(Start.UtcDateTime.AddHours(1), _tickets.Tickets.Single().ResponseDueAt);
    }

    [Fact]
    public async Task Create_WhenThePolicyChangesLater_KeepsTheDueTimes()
    {
        SetPolicy(TicketPriority.Mid, 240, 1440);
        var created = await CreateAsync("mid");

        SetPolicy(TicketPriority.Mid, 10, 20);
        var later = await _service.GetAsync(created.Id, CancellationToken.None);

        Assert.Equal(Start.UtcDateTime.AddHours(4), later.ResponseDueAt);
        Assert.Equal(Start.UtcDateTime.AddHours(24), later.ResolutionDueAt);
    }

    [Fact]
    public async Task ChangePriority_RecalculatesTheDueTimes()
    {
        SetPolicy(TicketPriority.Low, 480, 4320);
        SetPolicy(TicketPriority.High, 60, 240);
        var created = await CreateAsync("low");
        _clock.UtcNow = Start.AddMinutes(30);

        var changed = await _service.ChangePriorityAsync(created.Id, new ChangeTicketPriorityRequest("high"), CancellationToken.None);

        Assert.Equal("high", changed.Priority);
        Assert.Equal(Start.UtcDateTime.AddHours(1), changed.ResponseDueAt);
        Assert.Equal(Start.UtcDateTime.AddHours(4), changed.ResolutionDueAt);
        Assert.Equal(Start.UtcDateTime.AddMinutes(30), changed.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("urgent")]
    public async Task ChangePriority_WithAnInvalidPriority_ThrowsValidationException(string? priority)
    {
        var created = await CreateAsync("low");

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ChangePriorityAsync(created.Id, new ChangeTicketPriorityRequest(priority), CancellationToken.None));

        Assert.Equal(["priority"], error.Errors.Keys);
    }

    [Fact]
    public async Task ChangePriority_UnknownTicket_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangePriorityAsync(Guid.NewGuid(), new ChangeTicketPriorityRequest("high"), CancellationToken.None));
    }
}
