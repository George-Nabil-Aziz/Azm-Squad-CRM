using Crm.Application.Tickets;
using Crm.Domain.Sla;
using Crm.UnitTests.Settings;

namespace Crm.UnitTests.Tickets;

/// <summary>CRM-35: the ticket prefix and the business hours come from the system settings.</summary>
public class TicketServiceSettingsTests
{
    // Thursday 15:00 UTC.
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero));
    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeSystemSettingsProvider _settings = new();
    private readonly TicketService _service;
    private readonly Guid _customerId;

    public TicketServiceSettingsTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketService(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(Guid.NewGuid()), _clock,
            new CreateTicketRequestValidator(), new ListTicketsQueryValidator(),
            new Crm.UnitTests.Sla.FakeSlaPolicyRepository(_clock.UtcNow.UtcDateTime), new FakeTicketHistoryRecorder(), _settings);
        _customerId = _tickets.AddCustomer("Nour Trading");
    }

    private Task<TicketResponse> CreateAsync(string priority = "high") =>
        _service.CreateAsync(new CreateTicketRequest(_customerId, "Printer", null, null, priority), CancellationToken.None);

    private static BusinessCalendar WorkWeek() => new(
        [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday],
        new TimeOnly(8, 0), new TimeOnly(16, 0), TimeZoneInfo.Utc);

    [Fact]
    public async Task ANewTicket_GetsTheConfiguredPrefix()
    {
        _settings.TicketPrefix = "SUP-";

        var created = await CreateAsync();

        Assert.Equal("SUP-000001", created.Number);
        Assert.Equal("SUP-", _tickets.Tickets.Single().Prefix);
    }

    [Fact]
    public async Task ATicketCreatedBeforeThePrefixChanged_KeepsItsOwnPrefix()
    {
        var first = await CreateAsync();
        _settings.TicketPrefix = "SUP-";

        var second = await CreateAsync();

        Assert.Equal("TKT-000001", first.Number);
        Assert.Equal("SUP-000002", second.Number);
        Assert.Equal("TKT-000001", (await _service.GetAsync(first.Id, CancellationToken.None)).Number);
    }

    [Fact]
    public async Task WithoutBusinessHours_TheSlaClockRuns24x7()
    {
        await CreateAsync();

        Assert.Equal(_clock.UtcNow.UtcDateTime.AddMinutes(120), _tickets.Tickets.Single().ResponseDueAt);
    }

    [Fact]
    public async Task WithBusinessHours_TheDueTimesCountOnlyBusinessMinutes()
    {
        _settings.Calendar = WorkWeek();

        await CreateAsync();

        // Thursday 15:00: 60 minutes left, then Friday and Saturday are closed → Sunday 09:00 UTC.
        var ticket = _tickets.Tickets.Single();
        Assert.Equal(new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc), ticket.ResponseDueAt);
        Assert.Equal(new DateTime(2026, 10, 11, 15, 0, 0, DateTimeKind.Utc), ticket.ResolutionDueAt); // 480 min: 60 on Thursday + 420 on Sunday
    }

    [Fact]
    public async Task ChangingBusinessHours_DoesNotMoveExistingTickets_ButAffectsTheNextOne()
    {
        await CreateAsync();
        var firstDue = _tickets.Tickets.Single().ResponseDueAt;

        _settings.Calendar = WorkWeek();
        await CreateAsync();

        Assert.Equal(firstDue, _tickets.Tickets[0].ResponseDueAt);
        Assert.NotEqual(firstDue, _tickets.Tickets[1].ResponseDueAt);
    }

    [Fact]
    public async Task ChangingThePriority_RecalculatesWithTheBusinessHours()
    {
        _settings.Calendar = WorkWeek();
        var created = await CreateAsync("low"); // 8 h response = 480 business minutes

        await _service.ChangePriorityAsync(created.Id, new ChangeTicketPriorityRequest("high"), CancellationToken.None);

        Assert.Equal(new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc), _tickets.Tickets.Single().ResponseDueAt);
    }
}
