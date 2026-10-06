using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketPrefixTests
{
    private static Ticket NewTicket() => Ticket.Create(
        Guid.NewGuid(), "Printer", null, null, TicketPriority.Mid, TicketChannel.Manual, null,
        new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void ANewTicket_UsesTheDefaultPrefix()
    {
        var ticket = NewTicket();
        ticket.AssignNumber(7);

        Assert.Equal("TKT-", ticket.Prefix);
        Assert.Equal("TKT-000007", ticket.DisplayNumber);
    }

    [Fact]
    public void AssignNumber_WithAPrefix_ChangesTheDisplayNumber()
    {
        var ticket = NewTicket();

        ticket.AssignNumber(12, "SUP-");

        Assert.Equal("SUP-", ticket.Prefix);
        Assert.Equal("SUP-000012", ticket.DisplayNumber);
        Assert.Equal(12, ticket.Number);
    }

    [Theory]
    [InlineData(5, "SUP-", "SUP-000005")]
    [InlineData(1234567, "AZM-", "AZM-1234567")]
    public void FormatNumber_UsesTheGivenPrefix(int number, string prefix, string expected)
    {
        Assert.Equal(expected, Ticket.FormatNumber(number, prefix));
    }

    [Theory]
    [InlineData("SUP-000012", 12)]
    [InlineData("sup-12", 12)]
    [InlineData("AZM2-7", 7)]
    public void TryParseNumber_AcceptsAnyPrefix(string text, int expected)
    {
        Assert.True(Ticket.TryParseNumber(text, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("SUP-")]
    [InlineData("-SUP-5")]
    [InlineData("SU P-5")]
    [InlineData("SUP-5x")]
    [InlineData("SUP-0")]
    public void TryParseNumber_StillRejectsOtherText(string text)
    {
        Assert.False(Ticket.TryParseNumber(text, out _));
    }

    [Theory]
    [InlineData("SUP-", true)]
    [InlineData("SUP", true)]
    [InlineData("A1", true)]
    [InlineData("", false)]
    [InlineData("TOOLONGPREFIX1-", false)]
    [InlineData("SU P-", false)]
    [InlineData("SUP--", false)]
    public void IsValidPrefix_AcceptsOneToTenLettersOrDigits_WithAnOptionalDash(string prefix, bool valid)
    {
        Assert.Equal(valid, Ticket.IsValidPrefix(prefix));
    }

    [Theory]
    [InlineData("sup", "SUP-")]
    [InlineData(" SUP- ", "SUP-")]
    public void NormalizePrefix_UppercasesAndEndsWithADash(string prefix, string expected)
    {
        Assert.Equal(expected, Ticket.NormalizePrefix(prefix));
    }
}
