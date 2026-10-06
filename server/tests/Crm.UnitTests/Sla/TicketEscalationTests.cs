using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>CRM-22: warning and escalation rules of a ticket (no database).</summary>
public class TicketEscalationTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, null, Created);
        ticket.ApplySla(SlaPolicy.Create(TicketPriority.High, 100, 400, Created));
        return ticket;
    }

    [Fact]
    public void WarningAt_Is80PercentOfTheResponseWindow()
    {
        Assert.Equal(Created.AddMinutes(80), NewTicket().ResponseWarningAt);
    }

    [Fact]
    public void ChangePriority_RecalculatesTheWarningTime()
    {
        var ticket = NewTicket();

        ticket.ChangePriority(TicketPriority.Low, SlaPolicy.Create(TicketPriority.Low, 200, 800, Created), Created);

        Assert.Equal(Created.AddMinutes(160), ticket.ResponseWarningAt);
    }

    [Fact]
    public void TryWarn_Before80Percent_False()
    {
        var ticket = NewTicket();

        Assert.False(ticket.TryWarnResponse(Created.AddMinutes(79)));
        Assert.Null(ticket.ResponseWarnedAt);
    }

    [Fact]
    public void TryWarn_After80Percent_TrueOnce()
    {
        var ticket = NewTicket();

        Assert.True(ticket.TryWarnResponse(Created.AddMinutes(80)));
        Assert.False(ticket.TryWarnResponse(Created.AddMinutes(90)));
        Assert.Equal(Created.AddMinutes(80), ticket.ResponseWarnedAt);
    }

    [Fact]
    public void TryWarn_AfterTheDueTime_False()
    {
        Assert.False(NewTicket().TryWarnResponse(Created.AddMinutes(100)));
    }

    [Fact]
    public void TryWarn_WhenRespondedOrResolved_False()
    {
        var responded = NewTicket();
        responded.MarkFirstResponse(Created.AddMinutes(10));
        var resolved = NewTicket();
        resolved.MarkResolved(Created.AddMinutes(10));

        Assert.False(responded.TryWarnResponse(Created.AddMinutes(85)));
        Assert.False(resolved.TryWarnResponse(Created.AddMinutes(85)));
    }

    [Fact]
    public void TryWarn_WithoutSla_False()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Old ticket", null, null, TicketPriority.Mid, TicketChannel.Manual, null, Created);

        Assert.False(ticket.TryWarnResponse(Created.AddYears(1)));
    }

    [Fact]
    public void Escalate_IncrementsTheLevel()
    {
        var ticket = NewTicket();

        Assert.Equal(1, ticket.Escalate(Created.AddMinutes(101)));
        Assert.Equal(2, ticket.Escalate(Created.AddMinutes(500)));
        Assert.Equal(2, ticket.EscalationLevel);
        Assert.Equal(Created.AddMinutes(500), ticket.EscalatedAt);
    }

    [Fact]
    public void Escalate_WhenResolved_Throws()
    {
        var ticket = NewTicket();
        ticket.MarkResolved(Created.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() => ticket.Escalate(Created.AddMinutes(500)));
        Assert.Equal(0, ticket.EscalationLevel);
    }
}
