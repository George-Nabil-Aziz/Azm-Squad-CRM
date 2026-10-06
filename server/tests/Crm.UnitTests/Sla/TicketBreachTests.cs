using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

/// <summary>CRM-21: the breach rules of a ticket (no database).</summary>
public class TicketBreachTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(bool withSla = true)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, null, Created);
        if (withSla)
        {
            ticket.ApplySla(SlaPolicy.Create(TicketPriority.High, 60, 240, Created));
        }

        return ticket;
    }

    [Fact]
    public void NoReplyAfterResponseDue_IsResponseBreached()
    {
        var ticket = NewTicket();

        Assert.False(ticket.IsResponseBreachedAt(Created.AddMinutes(59)));
        Assert.True(ticket.IsResponseBreachedAt(Created.AddMinutes(60)));
    }

    [Fact]
    public void ReplyWithinResponseTime_IsNotBreached()
    {
        var ticket = NewTicket();
        ticket.MarkFirstResponse(Created.AddMinutes(30));

        Assert.False(ticket.IsResponseBreachedAt(Created.AddDays(30)));
    }

    [Fact]
    public void LateReply_IsResponseBreached()
    {
        var ticket = NewTicket();
        ticket.MarkFirstResponse(Created.AddMinutes(90));

        Assert.True(ticket.IsResponseBreachedAt(Created.AddMinutes(91)));
    }

    [Fact]
    public void ResolvedAfterResolutionDue_IsResolutionBreached()
    {
        var late = NewTicket();
        late.MarkResolved(Created.AddMinutes(300));
        var unresolved = NewTicket();

        Assert.True(late.IsResolutionBreachedAt(Created.AddMinutes(301)));
        Assert.True(unresolved.IsResolutionBreachedAt(Created.AddMinutes(240)));
        Assert.False(unresolved.IsResolutionBreachedAt(Created.AddMinutes(239)));
    }

    [Fact]
    public void ResolvedInTime_IsNotBreached()
    {
        var ticket = NewTicket();
        ticket.MarkResolved(Created.AddMinutes(200));

        Assert.False(ticket.IsResolutionBreachedAt(Created.AddDays(30)));
    }

    [Fact]
    public void MarkBreached_Twice_ReturnsFalseTheSecondTime()
    {
        var ticket = NewTicket();

        Assert.True(ticket.MarkResponseBreached());
        Assert.False(ticket.MarkResponseBreached());
        Assert.True(ticket.MarkResolutionBreached());
        Assert.False(ticket.MarkResolutionBreached());
        Assert.True(ticket.ResponseBreached);
        Assert.True(ticket.ResolutionBreached);
    }

    [Fact]
    public void NoDueTimes_NeverBreached()
    {
        var ticket = NewTicket(withSla: false);

        Assert.False(ticket.IsResponseBreachedAt(Created.AddYears(1)));
        Assert.False(ticket.IsResolutionBreachedAt(Created.AddYears(1)));
    }
}
