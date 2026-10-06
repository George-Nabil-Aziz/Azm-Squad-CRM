using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

public class TicketNumberTagTests
{
    [Theory]
    [InlineData(1, "[TKT-000001]")]
    [InlineData(123456, "[TKT-123456]")]
    [InlineData(1234567, "[TKT-1234567]")]
    public void Format_PadsToSixDigits(int number, string expected) =>
        Assert.Equal(expected, TicketNumberTag.Format(number));

    [Fact]
    public void AppendTo_AddsTheTag() =>
        Assert.Equal("Re: Printer broken [TKT-000001]", TicketNumberTag.AppendTo("Re: Printer broken", 1));

    [Fact]
    public void AppendTo_WithoutSubject_IsOnlyTheTag() =>
        Assert.Equal("[TKT-000042]", TicketNumberTag.AppendTo("  ", 42));

    [Fact]
    public void AppendTo_DoesNotRepeatAnExistingTag() =>
        Assert.Equal("Re: Re: Printer [TKT-000007]", TicketNumberTag.AppendTo("Re: Re: Printer [TKT-000007]", 7));

    [Theory]
    [InlineData("Re: help [TKT-000123]", 123)]
    [InlineData("[tkt-000005] question", 5)]
    [InlineData("Fwd: [TKT-1234567] more", 1234567)]
    public void TryFind_ReadsTheNumber(string subject, int expected)
    {
        Assert.True(TicketNumberTag.TryFind(subject, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TKT-000123 without brackets")]
    [InlineData("[TKT-] empty")]
    [InlineData("[TKT-000000] zero")]
    public void TryFind_WithoutTag_ReturnsFalse(string? subject) =>
        Assert.False(TicketNumberTag.TryFind(subject, out _));
}
