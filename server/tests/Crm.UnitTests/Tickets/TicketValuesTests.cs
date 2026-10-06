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

    [Fact]
    public void Statuses_AreTheWorkflowSteps_InOrder()
    {
        Assert.Equal(["new", "open", "pending", "resolved", "closed"], TicketValues.StatusNames);
        Assert.Equal(TicketValues.StatusNames, Enum.GetValues<TicketStatus>().Select(TicketValues.StatusName));
    }

    [Fact]
    public void Channels_HaveLowerCaseNames()
    {
        Assert.Equal(["manual", "email", "whatsapp", "portal"], Enum.GetValues<TicketChannel>().Select(TicketValues.ChannelName));
    }

    [Theory]
    [InlineData("Open", TicketStatus.Open)]
    [InlineData("resolved", TicketStatus.Resolved)]
    public void TryParseStatus_AcceptsNamesInAnyCase(string name, TicketStatus expected)
    {
        Assert.True(TicketValues.TryParseStatus(name, out var status));
        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("done")]
    public void TryParseStatus_RejectsOtherValues(string? name)
    {
        Assert.False(TicketValues.TryParseStatus(name, out _));
    }
}
