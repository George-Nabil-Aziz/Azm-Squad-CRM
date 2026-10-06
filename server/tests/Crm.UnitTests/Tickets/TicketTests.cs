using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Ticket NewTicket(string subject = "Invoice is wrong", Guid? customerId = null, DateTime? utcNow = null) =>
        Ticket.Create(customerId ?? CustomerId, subject, "Line 3 is charged twice.", null, TicketPriority.High,
            TicketChannel.Manual, null, utcNow ?? Now);

    [Fact]
    public void Create_StartsNew_Manual_WithTrimmedSubject_AndUtcTimes()
    {
        var categoryId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var ticket = Ticket.Create(CustomerId, "  Invoice is wrong ", "  ", categoryId, TicketPriority.Low,
            TicketChannel.Manual, creatorId, Now);

        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Equal(0, ticket.Number); // assigned when saved
        Assert.Equal("Invoice is wrong", ticket.Subject);
        Assert.Null(ticket.Description); // blank description = none
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Equal(TicketChannel.Manual, ticket.Channel);
        Assert.Equal(CustomerId, ticket.CustomerId);
        Assert.Equal(categoryId, ticket.CategoryId);
        Assert.Null(ticket.AssigneeId);
        Assert.Equal(creatorId, ticket.CreatedById);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.Equal(Now, ticket.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankSubject_Throws(string subject)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewTicket(subject));
    }

    [Fact]
    public void Create_WithEmptyCustomerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewTicket(customerId: Guid.Empty));
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewTicket(utcNow: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }

    [Theory]
    [InlineData(1, "TKT-000001")]
    [InlineData(42, "TKT-000042")]
    [InlineData(1234567, "TKT-1234567")]
    public void FormatNumber_PadsToSixDigits(int number, string expected)
    {
        Assert.Equal(expected, Ticket.FormatNumber(number));
    }

    [Theory]
    [InlineData("TKT-000012", 12)]
    [InlineData("tkt-12", 12)]
    [InlineData(" 000012 ", 12)]
    [InlineData("12", 12)]
    public void TryParseNumber_AcceptsPrefixedAndPlainNumbers(string text, int expected)
    {
        Assert.True(Ticket.TryParseNumber(text, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("TKT-")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("TKT-12a")]
    public void TryParseNumber_RejectsOtherText(string text)
    {
        Assert.False(Ticket.TryParseNumber(text, out _));
    }

    [Fact]
    public void AssignNumber_SetsTheDisplayNumber_AndRejectsZeroOrNegative()
    {
        var ticket = NewTicket();

        ticket.AssignNumber(7);

        Assert.Equal(7, ticket.Number);
        Assert.Equal("TKT-000007", ticket.DisplayNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() => ticket.AssignNumber(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ticket.AssignNumber(-1));
    }

    [Fact]
    public void RecordAgentReply_SetsFirstResponseAtOnce_AndBumpsUpdatedAt()
    {
        var ticket = NewTicket();
        Assert.Null(ticket.FirstResponseAt);
        var first = Now.AddMinutes(5);
        var second = Now.AddMinutes(30);

        ticket.RecordAgentReply(first);
        ticket.RecordAgentReply(second);

        Assert.Equal(first, ticket.FirstResponseAt);
        Assert.Equal(DateTimeKind.Utc, ticket.FirstResponseAt!.Value.Kind);
        Assert.Equal(second, ticket.UpdatedAt);
    }

    [Fact]
    public void RecordAgentReply_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => NewTicket().RecordAgentReply(DateTime.SpecifyKind(Now, DateTimeKind.Local)));

    [Fact]
    public void AssignTo_SetsTheAssignee_AndBumpsUpdatedAt_ReturningTrue()
    {
        var ticket = NewTicket();
        var agentId = Guid.NewGuid();
        var later = Now.AddMinutes(10);

        var changed = ticket.AssignTo(agentId, later);

        Assert.True(changed);
        Assert.Equal(agentId, ticket.AssigneeId);
        Assert.Equal(later, ticket.UpdatedAt);
    }

    [Fact]
    public void AssignTo_TheSameAssignee_ChangesNothing_ReturningFalse()
    {
        var ticket = NewTicket();
        var agentId = Guid.NewGuid();
        ticket.AssignTo(agentId, Now.AddMinutes(10));

        var changed = ticket.AssignTo(agentId, Now.AddMinutes(20));

        Assert.False(changed);
        Assert.Equal(Now.AddMinutes(10), ticket.UpdatedAt);
    }

    [Fact]
    public void AssignTo_Null_Unassigns_AndNonUtcTimeThrows()
    {
        var ticket = NewTicket();
        ticket.AssignTo(Guid.NewGuid(), Now);

        Assert.True(ticket.AssignTo(null, Now.AddMinutes(1)));
        Assert.Null(ticket.AssigneeId);
        Assert.Throws<ArgumentException>(() => ticket.AssignTo(Guid.NewGuid(), DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void ChangeStatus_MovesTheTicket_AndBumpsUpdatedAt()
    {
        var ticket = NewTicket();

        ticket.ChangeStatus(TicketStatus.Open, Now.AddMinutes(5));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(Now.AddMinutes(5), ticket.UpdatedAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void ChangeStatus_ToResolved_SetsResolvedAt_AndOtherMovesKeepIt()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Open, Now);
        var resolvedTime = Now.AddHours(1);

        ticket.ChangeStatus(TicketStatus.Resolved, resolvedTime);
        ticket.ChangeStatus(TicketStatus.Closed, Now.AddDays(1));

        Assert.Equal(resolvedTime, ticket.ResolvedAt);
        Assert.Equal(DateTimeKind.Utc, ticket.ResolvedAt!.Value.Kind);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Reopen_ClearsResolvedAt(TicketStatus from)
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Open, Now);
        ticket.ChangeStatus(TicketStatus.Resolved, Now.AddHours(1));
        if (from == TicketStatus.Closed)
        {
            ticket.ChangeStatus(TicketStatus.Closed, Now.AddHours(2));
        }

        ticket.ChangeStatus(TicketStatus.Open, Now.AddHours(3));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void ChangeStatus_WithAnIllegalMove_ThrowsAndChangesNothing()
    {
        var ticket = NewTicket();
        TicketTestSupport.SetStatus(ticket, TicketStatus.Closed);

        var error = Assert.Throws<InvalidTicketStatusTransitionException>(() => ticket.ChangeStatus(TicketStatus.Pending, Now.AddMinutes(1)));

        Assert.Equal((TicketStatus.Closed, TicketStatus.Pending), (error.From, error.To));
        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(Now, ticket.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ToTheSameStatus_Throws() =>
        Assert.Throws<InvalidTicketStatusTransitionException>(() => NewTicket().ChangeStatus(TicketStatus.New, Now));

    [Fact]
    public void ChangeStatus_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => NewTicket().ChangeStatus(TicketStatus.Open, DateTime.SpecifyKind(Now, DateTimeKind.Local)));

    [Fact]
    public void AcceptsMessages_IsFalseOnlyWhenClosed()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            var ticket = NewTicket();
            TicketTestSupport.SetStatus(ticket, status);

            Assert.Equal(status != TicketStatus.Closed, ticket.AcceptsMessages);
        }
    }
}
