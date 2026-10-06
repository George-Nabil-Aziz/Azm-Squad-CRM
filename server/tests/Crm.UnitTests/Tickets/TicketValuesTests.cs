using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketValuesTests
{
    [Fact]
    public void Priorities_AreExactlyHighMidLow()
    {
        Assert.Equal([TicketPriority.High, TicketPriority.Mid, TicketPriority.Low], Enum.GetValues<TicketPriority>());
        Assert.Equal(["high", "mid", "low"], TicketValues.PriorityNames);
    }

    [Theory]
    [InlineData("high", TicketPriority.High)]
    [InlineData("Mid", TicketPriority.Mid)]
    [InlineData("LOW", TicketPriority.Low)]
    public void TryParsePriority_AcceptsNamesInAnyCase(string name, TicketPriority expected)
    {
        Assert.True(TicketValues.TryParsePriority(name, out var priority));
        Assert.Equal(expected, priority);
        Assert.Equal(name.ToLowerInvariant(), TicketValues.PriorityName(priority));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("urgent")]
    public void TryParsePriority_RejectsOtherValues(string? name)
    {
        Assert.False(TicketValues.TryParsePriority(name, out _));
    }
}
