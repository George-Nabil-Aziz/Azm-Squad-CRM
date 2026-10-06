using Crm.Application.Tickets;

namespace Crm.UnitTests.Tickets;

public class AutoAssignmentRulesTests
{
    [Fact]
    public void Pick_ReturnsFewestOpenTickets()
    {
        var busy = new AssignmentCandidate(Guid.NewGuid(), "Busy", 5);
        var free = new AssignmentCandidate(Guid.NewGuid(), "Free", 1);

        Assert.Equal(free, AutoAssignmentRules.Pick([busy, free]));
    }

    [Fact]
    public void Pick_TieBreaksByNameThenId()
    {
        var omar = new AssignmentCandidate(Guid.NewGuid(), "Omar", 2);
        var sara = new AssignmentCandidate(Guid.NewGuid(), "Sara", 2);

        Assert.Equal(omar, AutoAssignmentRules.Pick([sara, omar]));
    }

    [Fact]
    public void Pick_NoCandidates_ReturnsNull() => Assert.Null(AutoAssignmentRules.Pick([]));
}
