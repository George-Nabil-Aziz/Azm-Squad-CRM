using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeInteractionRecorder _timeline = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketService _service;
    private readonly Guid _customerId;
    private readonly TicketCategory _billing;

    public TicketServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketService(_tickets, _categories, _timeline, new FakeCurrentUser(AgentId), _clock,
            new CreateTicketRequestValidator(), new ListTicketsQueryValidator());
        _customerId = _tickets.AddCustomer("Nour Trading");
        _billing = TicketCategory.Create("Billing", _clock.UtcNow.UtcDateTime);
        _categories.Add(_billing);
    }

    private CreateTicketRequest Request(string? subject = "Invoice is wrong", Guid? categoryId = null, string? priority = "high") =>
        new(_customerId, subject, "Line 3 is charged twice.", categoryId ?? _billing.Id, priority);

    [Fact]
    public async Task Create_WithAllFields_ReturnsNumberStatusNewAndUtcCreatedAt()
    {
        var response = await _service.CreateAsync(Request(), CancellationToken.None);

        var saved = Assert.Single(_tickets.Tickets);
        Assert.Equal(saved.Id, response.Id);
        Assert.Equal("TKT-000001", response.Number);
        Assert.Equal("new", response.Status);
        Assert.Equal("high", response.Priority);
        Assert.Equal("manual", response.Channel);
        Assert.Equal("Invoice is wrong", response.Subject);
        Assert.Equal("Line 3 is charged twice.", response.Description);
        Assert.Equal(_customerId, response.CustomerId);
        Assert.Equal("Nour Trading", response.CustomerName);
        Assert.Equal(_billing.Id, response.CategoryId);
        Assert.Equal("Billing", response.CategoryName);
        Assert.Null(response.AssigneeId);
        Assert.Equal(_clock.UtcNow.UtcDateTime, response.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, response.CreatedAt.Kind);
    }

    [Fact]
    public async Task Create_WithoutCustomerOrSubject_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new CreateTicketRequest(null, "  ", null, null, null), CancellationToken.None));

        Assert.Equal(["customerId", "subject"], error.Errors.Keys.Order());
        Assert.Empty(_tickets.Tickets);
    }

    [Fact]
    public async Task Create_ForAnUnknownCustomer_ThrowsValidationException_OnCustomerId()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request() with { CustomerId = Guid.NewGuid() }, CancellationToken.None));

        Assert.Equal(["customerId"], error.Errors.Keys);
    }

    [Fact]
    public async Task Create_WithAnInactiveOrUnknownCategory_ThrowsValidationException_OnCategoryId()
    {
        var legacy = TicketCategory.Create("Legacy", _clock.UtcNow.UtcDateTime);
        legacy.Update("Legacy", isActive: false, _clock.UtcNow.UtcDateTime);
        _categories.Add(legacy);

        var inactive = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(categoryId: legacy.Id), CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(categoryId: Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(["categoryId"], inactive.Errors.Keys);
        Assert.Equal(["categoryId"], unknown.Errors.Keys);
        Assert.Empty(_tickets.Tickets);
    }

    [Fact]
    public async Task Create_WithoutACategory_IsAllowed()
    {
        var response = await _service.CreateAsync(Request() with { CategoryId = null }, CancellationToken.None);

        Assert.Null(response.CategoryId);
        Assert.Null(response.CategoryName);
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("1")]
    public async Task Create_WithAnInvalidPriority_ThrowsValidationException_OnPriority(string priority)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(priority: priority), CancellationToken.None));

        Assert.Equal(["priority"], error.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_WithoutPriority_UsesMid(string? priority)
    {
        var response = await _service.CreateAsync(Request(priority: priority), CancellationToken.None);

        Assert.Equal("mid", response.Priority);
    }

    [Fact]
    public async Task Create_RecordsTheSignedInUserAsCreator()
    {
        await _service.CreateAsync(Request(), CancellationToken.None);

        Assert.Equal(AgentId, Assert.Single(_tickets.Tickets).CreatedById);
    }

    [Fact]
    public async Task Create_TwoTickets_GetConsecutiveNumbers()
    {
        var first = await _service.CreateAsync(Request(), CancellationToken.None);
        var second = await _service.CreateAsync(Request("Second"), CancellationToken.None);

        Assert.Equal("TKT-000001", first.Number);
        Assert.Equal("TKT-000002", second.Number);
    }

    [Fact]
    public async Task Create_RecordsATicketCreatedTimelineEntry_SavedWithTheTicket()
    {
        var response = await _service.CreateAsync(Request(), CancellationToken.None);

        var entry = Assert.Single(_timeline.Entries);
        Assert.Equal((_customerId, InteractionType.Ticket, InteractionEvents.TicketCreated, "TKT-000001 Invoice is wrong", (Guid?)response.Id),
            (entry.CustomerId, entry.Type, entry.Event, entry.Details, entry.SourceId));
        Assert.Equal(_clock.UtcNow.UtcDateTime, entry.UtcNow);
        Assert.Equal(1, _tickets.SaveCount); // one save: the ticket and its timeline entry together
    }

    [Fact]
    public async Task Create_InvalidRequest_RecordsNoTimelineEntry()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(Request(subject: ""), CancellationToken.None));

        Assert.Empty(_timeline.Entries);
    }

    [Fact]
    public async Task Create_ConcurrentRequests_GetDistinctConsecutiveNumbers()
    {
        var created = await Task.WhenAll(Enumerable.Range(1, 10)
            .Select(n => _service.CreateAsync(Request($"Ticket {n}"), CancellationToken.None)));

        Assert.Equal(Enumerable.Range(1, 10).Select(Ticket.FormatNumber), created.Select(t => t.Number).Order());
    }

    [Fact]
    public async Task Get_ReturnsTheTicket()
    {
        var created = await _service.CreateAsync(Request(), CancellationToken.None);

        var found = await _service.GetAsync(created.Id, CancellationToken.None);

        Assert.Equal(created with { AllowedStatuses = found.AllowedStatuses }, found);
        Assert.Equal(created.AllowedStatuses, found.AllowedStatuses);
    }

    [Fact]
    public async Task Get_UnknownTicket_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
