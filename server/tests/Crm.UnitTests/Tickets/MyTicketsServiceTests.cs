using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

internal sealed class FakeMyTicketsRepository(FakeTicketRepository tickets) : IMyTicketsRepository
{
    public int BreachedToday { get; set; }

    public (DateTime Start, DateTime End)? LastDay { get; private set; }

    public Task<IReadOnlyList<TicketView>> ListOpenAssignedAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketView>>([.. tickets.Tickets
            .Where(t => t.AssigneeId == userId && t.Status != TicketStatus.Closed)
            .Select(tickets.View)]);

    public Task<int> CountBreachedTodayAsync(Guid userId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken cancellationToken)
    {
        LastDay = (dayStartUtc, dayEndUtc);
        return Task.FromResult(BreachedToday);
    }
}

public class MyTicketsServiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeMyTicketsRepository _repository;
    private readonly Guid _customer;
    private int _number;

    public MyTicketsServiceTests()
    {
        _repository = new FakeMyTicketsRepository(_tickets);
        _customer = _tickets.AddCustomer("Nour");
    }

    private MyTicketsService Service(Guid? user = null) =>
        new(_repository, new FakeCurrentUser(user ?? Me), new TestClock(new DateTimeOffset(Now)), new MyTicketsQueryValidator());

    private Ticket Add(Guid? assignee, TicketPriority priority, int? responseMinutes, TicketStatus status = TicketStatus.Open)
    {
        var ticket = Ticket.Create(_customer, $"T{_number}", null, null, priority, TicketChannel.Manual, null, Now.AddMinutes(_number));
        ticket.AssignNumber(++_number);
        if (responseMinutes is { } minutes)
        {
            ticket.ApplySla(SlaPolicy.Create(priority, minutes, minutes * 4, ticket.CreatedAt));
        }

        if (assignee is not null)
        {
            ticket.AssignTo(assignee, Now);
        }

        TicketTestSupport.SetStatus(ticket, status);
        _tickets.Tickets.Add(ticket);
        return ticket;
    }

    private Task<MyTicketsResponse> GetAsync(Guid? user = null, int? page = null, int? pageSize = null) =>
        Service(user).GetAsync(new MyTicketsQuery(page, pageSize), CancellationToken.None);

    [Fact]
    public async Task ReturnsOnlyMyTickets_ThatAreNotClosed()
    {
        var mine = Add(Me, TicketPriority.Mid, 60);
        var resolved = Add(Me, TicketPriority.Mid, 60, TicketStatus.Resolved);
        Add(Me, TicketPriority.Mid, 60, TicketStatus.Closed);
        Add(Other, TicketPriority.Mid, 60);
        Add(null, TicketPriority.Mid, 60);

        var response = await GetAsync();

        Assert.Equal(new[] { mine.Id, resolved.Id }.Order(), response.Tickets.Items.Select(t => t.Id).Order());
    }

    [Fact]
    public async Task SortsByTheNearestSlaDue_TicketsWithoutADueTimeLast()
    {
        var late = Add(Me, TicketPriority.Low, 300);
        var none = Add(Me, TicketPriority.Low, null);
        var soon = Add(Me, TicketPriority.High, 30);
        var resolved = Add(Me, TicketPriority.High, 30);
        resolved.MarkFirstResponse(Now);
        resolved.MarkResolved(Now);

        var response = await GetAsync();

        // The resolved ticket has no due time any more, like the one without a policy; creation order breaks the tie.
        Assert.Equal([soon.Id, late.Id, none.Id, resolved.Id], response.Tickets.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task Counters_CountOpenPendingAndBreachedToday()
    {
        Add(Me, TicketPriority.Mid, 60, TicketStatus.New);
        Add(Me, TicketPriority.Mid, 60, TicketStatus.Open);
        Add(Me, TicketPriority.Mid, 60, TicketStatus.Pending);
        Add(Me, TicketPriority.Mid, 60, TicketStatus.Resolved);
        _repository.BreachedToday = 2;

        var counters = (await GetAsync()).Counters;

        Assert.Equal((2, 1, 2), (counters.Open, counters.Pending, counters.BreachedToday));
        Assert.Equal((new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)), _repository.LastDay);
    }

    [Fact]
    public async Task Pages_TheSortedTickets()
    {
        for (var i = 0; i < 5; i++)
        {
            Add(Me, TicketPriority.Mid, 10 + i);
        }

        var second = await GetAsync(page: 2, pageSize: 2);

        Assert.Equal((5, 2, 2), (second.Tickets.TotalCount, second.Tickets.Page, second.Tickets.Items.Count));
        Assert.Equal(["T2", "T3"], second.Tickets.Items.Select(t => t.Subject));
    }

    [Fact]
    public async Task APageSizeOverTheMaximum_IsAValidationError() =>
        await Assert.ThrowsAsync<ValidationException>(() => GetAsync(pageSize: 500));
}
