using Crm.Domain.Tickets;
using static Crm.Domain.Tickets.TicketStatus;

namespace Crm.UnitTests.Tickets;

public class TicketStatusRulesTests
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Expected = new()
    {
        [New] = [Open],
        [Open] = [Pending, Resolved],
        [Pending] = [Open, Resolved],
        [Resolved] = [Open, Closed],
        [Closed] = [Open],
    };

    public static TheoryData<TicketStatus, TicketStatus, bool> AllPairs()
    {
        var data = new TheoryData<TicketStatus, TicketStatus, bool>();
        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                data.Add(from, to, Expected[from].Contains(to));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void CanMove_FollowsTheWorkflowTable(TicketStatus from, TicketStatus to, bool allowed) =>
        Assert.Equal(allowed, TicketStatusRules.CanMove(from, to));

    [Fact]
    public void AllowedTargets_ListsTheTargetsInWorkflowOrder()
    {
        Assert.Equal([Pending, Resolved], TicketStatusRules.AllowedTargets(Open));
        Assert.Equal([Open, Closed], TicketStatusRules.AllowedTargets(Resolved));
        Assert.Equal([Open], TicketStatusRules.AllowedTargets(Closed));
    }

    [Fact]
    public void ClosedToPending_IsNotAllowed() => Assert.False(TicketStatusRules.CanMove(Closed, Pending));
}
