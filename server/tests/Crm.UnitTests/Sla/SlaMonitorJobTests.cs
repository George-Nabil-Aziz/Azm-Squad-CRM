using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>CRM-21: the SLA monitor job, with an in-memory repository and a controllable clock.</summary>
public class SlaMonitorJobTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTicketSlaRepository _repository = new();
    private readonly TestClock _clock = new(Start);
    private readonly SlaMonitorJob _job;

    public SlaMonitorJobTests() => _job = new SlaMonitorJob(_repository, _clock);

    private Ticket AddTicket()
    {
        var created = Start.UtcDateTime;
        var ticket = Ticket.Create(Guid.NewGuid(), "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, null, created);
        ticket.ApplySla(SlaPolicy.Create(TicketPriority.High, 60, 240, created));
        _repository.Tickets.Add(ticket);
        return ticket;
    }

    [Fact]
    public async Task Run_MarksBreachesAndAddsOneEventPerKind()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(300);

        var result = await _job.RunAsync(CancellationToken.None);

        Assert.True(ticket.ResponseBreached);
        Assert.True(ticket.ResolutionBreached);
        Assert.Equal(new SlaMonitorResult(1, 1), result);
        Assert.Equal([SlaEventType.ResponseBreached, SlaEventType.ResolutionBreached], _repository.Events.Select(e => e.Type).Order());
        Assert.All(_repository.Events, e => Assert.Equal(ticket.Id, e.TicketId));
        Assert.Equal(ticket.ResponseDueAt, _repository.Events.Single(e => e.Type == SlaEventType.ResponseBreached).DueAt);
    }

    [Fact]
    public async Task Run_Twice_AddsNoDuplicateEvents()
    {
        AddTicket();
        _clock.UtcNow = Start.AddMinutes(90);

        await _job.RunAsync(CancellationToken.None);
        var second = await _job.RunAsync(CancellationToken.None);

        Assert.Single(_repository.Events);
        Assert.Equal(new SlaMonitorResult(0, 0), second);
    }

    [Fact]
    public async Task Run_UsesTheClock()
    {
        var ticket = AddTicket();
        _clock.UtcNow = Start.AddMinutes(59);
        await _job.RunAsync(CancellationToken.None);
        Assert.False(ticket.ResponseBreached);

        _clock.UtcNow = Start.AddMinutes(60);
        await _job.RunAsync(CancellationToken.None);

        Assert.True(ticket.ResponseBreached);
        Assert.Equal(_clock.UtcNow.UtcDateTime, _repository.Events.Single().OccurredAt);
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
}

/// <summary>In-memory SLA storage with the same candidate rules as the EF Core repository.</summary>
internal sealed class FakeTicketSlaRepository : ITicketSlaRepository
{
    public List<Ticket> Tickets { get; } = [];

    public List<TicketSlaEvent> Events { get; } = [];

    public Task<IReadOnlyList<Ticket>> ListBreachCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Ticket>>([.. Tickets
            .Where(t => (!t.ResponseBreached && t.IsResponseBreachedAt(utcNow)) || (!t.ResolutionBreached && t.IsResolutionBreachedAt(utcNow)))
            .Take(take)]);

    public void AddEvent(TicketSlaEvent slaEvent) => Events.Add(slaEvent);

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
