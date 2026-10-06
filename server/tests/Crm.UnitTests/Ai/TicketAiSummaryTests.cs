using Crm.Domain.Ai;

namespace Crm.UnitTests.Ai;

public class TicketAiSummaryTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public void Create_KeepsTextLanguageAuthorAndTime()
    {
        var summary = TicketAiSummary.Create(TicketId, "  - Invoice is wrong  ", "en", AgentId, T0);

        Assert.Equal(TicketId, summary.TicketId);
        Assert.Equal("- Invoice is wrong", summary.Text);
        Assert.Equal(("en", AgentId, T0), (summary.Language, summary.GeneratedById, summary.GeneratedAt));
    }

    [Fact]
    public void Replace_OverwritesTheTextAndMovesTheTimestamp()
    {
        var summary = TicketAiSummary.Create(TicketId, "old", "en", AgentId, T0);
        var other = Guid.NewGuid();

        summary.Replace("new", "ar", other, T0.AddHours(1));

        Assert.Equal(("new", "ar", other, T0.AddHours(1)), (summary.Text, summary.Language, summary.GeneratedById, summary.GeneratedAt));
    }

    [Fact]
    public void TooLongText_IsCutToTheMaximum()
    {
        var summary = TicketAiSummary.Create(TicketId, new string('x', TicketAiSummary.TextMaxLength + 50), "en", null, T0);

        Assert.Equal(TicketAiSummary.TextMaxLength, summary.Text.Length);
    }

    [Fact]
    public void BlankText_NonUtcTime_AndEmptyTicket_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => TicketAiSummary.Create(TicketId, " ", "en", null, T0));
        Assert.Throws<ArgumentException>(() => TicketAiSummary.Create(TicketId, "x", "en", null, DateTime.SpecifyKind(T0, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => TicketAiSummary.Create(Guid.Empty, "x", "en", null, T0));
    }
}
