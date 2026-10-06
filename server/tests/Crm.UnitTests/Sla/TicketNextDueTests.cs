using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>CRM-29: the "nearest SLA due time" of a ticket.</summary>
public class TicketNextDueTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(bool withPolicy = true)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Printer", null, null, TicketPriority.High, TicketChannel.Manual, null, Created);
        if (withPolicy)
        {
            ticket.ApplySla(SlaPolicy.Create(TicketPriority.High, 120, 480, Created));
        }

        return ticket;
    }

    [Fact]
    public void NextSlaDueAt_BeforeTheFirstResponse_IsTheEarlierDue() =>
        Assert.Equal(Created.AddMinutes(120), NewTicket().NextSlaDueAt);

    [Fact]
    public void NextSlaDueAt_AfterTheFirstResponse_IsTheResolutionDue()
    {
        var ticket = NewTicket();
        ticket.MarkFirstResponse(Created.AddMinutes(10));

        Assert.Equal(Created.AddMinutes(480), ticket.NextSlaDueAt);
    }

    [Fact]
    public void NextSlaDueAt_WhenResolved_IsNull()
    {
        var ticket = NewTicket();
        ticket.MarkFirstResponse(Created.AddMinutes(10));
        ticket.MarkResolved(Created.AddMinutes(30));

        Assert.Null(ticket.NextSlaDueAt);
    }

    [Fact]
    public void NextSlaDueAt_WithoutPolicy_IsNull() => Assert.Null(NewTicket(withPolicy: false).NextSlaDueAt);
}
