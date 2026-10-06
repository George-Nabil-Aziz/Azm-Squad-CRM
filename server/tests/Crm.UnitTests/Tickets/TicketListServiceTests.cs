using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

/// <summary>CRM-14: TicketService.ListAsync turns the query string into a typed filter (the filtering itself is integration-tested).</summary>
public class TicketListServiceTests
{
    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketService _service;

    public TicketListServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketService(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(Guid.NewGuid()),
            _clock, new CreateTicketRequestValidator(), new ListTicketsQueryValidator(),
            new Crm.UnitTests.Sla.FakeSlaPolicyRepository(_clock.UtcNow.UtcDateTime), new FakeTicketHistoryRecorder());
    }

    private static ListTicketsQuery Query(
        string? status = null, string? priority = null, Guid? categoryId = null, Guid? assigneeId = null,
        bool? unassigned = null, DateOnly? from = null, DateOnly? to = null, string? search = null,
        int? page = null, int? pageSize = null) =>
        new(status, priority, categoryId, assigneeId, unassigned, from, to, search, page, pageSize);

    [Fact]
    public async Task List_PassesTypedFilters_AndPaging_ToTheRepository()
    {
        var categoryId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        await _service.ListAsync(
            Query("Open", "high", categoryId, assigneeId, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5),
                "  TKT-000012 ", 2, 10),
            CancellationToken.None);

        Assert.Equal(
            new TicketListFilter(TicketStatus.Open, TicketPriority.High, categoryId, assigneeId, false,
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
                "TKT-000012", 12),
            _tickets.LastFilter);
        Assert.Equal((2, 10), _tickets.LastPaging);
        Assert.Equal(DateTimeKind.Utc, _tickets.LastFilter!.CreatedFromUtc!.Value.Kind);
    }

    [Fact]
    public async Task List_WithoutParameters_UsesDefaultPaging_AndNoFilters()
    {
        await _service.ListAsync(Query(search: "   "), CancellationToken.None);

        Assert.Equal(new TicketListFilter(null, null, null, null, false, null, null, null, null), _tickets.LastFilter);
        Assert.Equal((1, 20), _tickets.LastPaging);
    }

    [Fact]
    public async Task List_SearchText_IsNoNumber()
    {
        await _service.ListAsync(Query(search: "printer", unassigned: true), CancellationToken.None);

        Assert.Equal(("printer", (int?)null, true), (_tickets.LastFilter!.Search, _tickets.LastFilter.SearchNumber, _tickets.LastFilter.Unassigned));
    }

    [Fact]
    public async Task List_MapsViewsToResponses_NewestFirst()
    {
        var customerId = _tickets.AddCustomer("Nour Trading");
        await _service.CreateAsync(new CreateTicketRequest(customerId, "First", null, null, "low"), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        await _service.CreateAsync(new CreateTicketRequest(customerId, "Second", null, null, null), CancellationToken.None);

        var page = await _service.ListAsync(Query(), CancellationToken.None);

        Assert.Equal(["Second", "First"], page.Items.Select(t => t.Subject));
        Assert.Equal(("TKT-000002", "mid", "new", "Nour Trading"),
            (page.Items[0].Number, page.Items[0].Priority, page.Items[0].Status, page.Items[0].CustomerName));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task List_InvalidQuery_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(Query(status: "done", pageSize: 500), CancellationToken.None));

        Assert.Equal(["pageSize", "status"], error.Errors.Keys.Order());
        Assert.Null(_tickets.LastFilter);
    }

    [Fact]
    public async Task ListAssignees_ReturnsTheRepositoryList()
    {
        _tickets.Assignees.Add(new TicketAssigneeResponse(Guid.NewGuid(), "Sara Agent"));

        var assignees = await _service.ListAssigneesAsync(CancellationToken.None);

        Assert.Equal(["Sara Agent"], assignees.Select(a => a.FullName));
    }
}
