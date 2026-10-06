using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

public class TicketSlaTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(TicketPriority priority = TicketPriority.High) =>
        Ticket.Create(Guid.NewGuid(), "Invoice is wrong", null, null, priority, TicketChannel.Manual, null, Created);

    private static SlaPolicy Policy(TicketPriority priority, int response, int resolution) =>
        SlaPolicy.Create(priority, response, resolution, Created);

    [Fact]
    public void NewTicket_HasNoSlaTimesYet()
    {
        var ticket = NewTicket();

        Assert.Null(ticket.ResponseDueAt);
        Assert.Null(ticket.ResolutionDueAt);
        Assert.Null(ticket.FirstResponseAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void ApplySla_SetsDueTimesFromCreatedAt()
    {
        var ticket = NewTicket();

        ticket.ApplySla(Policy(TicketPriority.High, 60, 240));

        Assert.Equal(Created.AddHours(1), ticket.ResponseDueAt);
        Assert.Equal(Created.AddHours(4), ticket.ResolutionDueAt);
    }

    [Fact]
    public void ApplySla_WithThePolicyOfAnotherPriority_Throws()
    {
        var ticket = NewTicket(TicketPriority.Low);

        Assert.Throws<ArgumentException>(() => ticket.ApplySla(Policy(TicketPriority.High, 60, 240)));
    }

    [Fact]
    public void ChangePriority_RecalculatesFromCreatedAt()
    {
        var ticket = NewTicket(TicketPriority.Low);
        ticket.ApplySla(Policy(TicketPriority.Low, 480, 4320));
        var later = Created.AddHours(2);

        ticket.ChangePriority(TicketPriority.High, Policy(TicketPriority.High, 60, 240), later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(Created.AddHours(1), ticket.ResponseDueAt);
        Assert.Equal(Created.AddHours(4), ticket.ResolutionDueAt);
        Assert.Equal(later, ticket.UpdatedAt);
    }

    [Fact]
    public void ChangePriority_ToTheSamePriority_KeepsTheDueTimes()
    {
        var ticket = NewTicket(TicketPriority.High);
        ticket.ApplySla(Policy(TicketPriority.High, 60, 240));

        ticket.ChangePriority(TicketPriority.High, Policy(TicketPriority.High, 10, 20), Created.AddHours(1));

        Assert.Equal(Created.AddHours(1), ticket.ResponseDueAt);
        Assert.Equal(Created, ticket.UpdatedAt);
    }

    [Fact]
    public void ChangePriority_WithoutAPolicy_ClearsNothingButChangesThePriority()
    {
        var ticket = NewTicket(TicketPriority.High);
        ticket.ApplySla(Policy(TicketPriority.High, 60, 240));

        ticket.ChangePriority(TicketPriority.Mid, null, Created.AddHours(1));

        Assert.Equal(TicketPriority.Mid, ticket.Priority);
        Assert.Equal(Created.AddHours(1), ticket.ResponseDueAt);
    }

    [Fact]
    public void MarkFirstResponse_KeepsTheFirstTime()
    {
        var ticket = NewTicket();

        ticket.MarkFirstResponse(Created.AddMinutes(10));
        ticket.MarkFirstResponse(Created.AddMinutes(50));

        Assert.Equal(Created.AddMinutes(10), ticket.FirstResponseAt);
    }

    [Fact]
    public void MarkResolved_ThenReopen_ClearsResolvedAt()
    {
        var ticket = NewTicket();

        ticket.MarkResolved(Created.AddHours(3));
        Assert.Equal(Created.AddHours(3), ticket.ResolvedAt);

        ticket.Reopen();
        Assert.Null(ticket.ResolvedAt);
    }
}
