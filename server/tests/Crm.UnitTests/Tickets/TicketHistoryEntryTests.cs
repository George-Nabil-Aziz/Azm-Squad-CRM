using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketHistoryEntryTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_KeepsOldAndNewValue_User_AndUtcTime()
    {
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var entry = TicketHistoryEntry.Create(ticketId, TicketHistoryField.Assignee, "Omar Lead", "Sara Agent", userId, Now);

        Assert.Equal(ticketId, entry.TicketId);
        Assert.Equal(TicketHistoryField.Assignee, entry.Field);
        Assert.Equal("Omar Lead", entry.OldValue);
        Assert.Equal("Sara Agent", entry.NewValue);
        Assert.Equal(userId, entry.ChangedById);
        Assert.Equal(Now, entry.ChangedAt);
    }

    [Fact]
    public void Create_AllowsNoOldValue_NoNewValue_AndNoUser()
    {
        var entry = TicketHistoryEntry.Create(Guid.NewGuid(), TicketHistoryField.Category, null, "  ", null, Now);

        Assert.Null(entry.OldValue);
        Assert.Null(entry.NewValue); // blank = none
        Assert.Null(entry.ChangedById);
    }

    [Fact]
    public void Create_ShortensVeryLongValues()
    {
        var entry = TicketHistoryEntry.Create(Guid.NewGuid(), TicketHistoryField.Category, new string('a', 500), "x", null, Now);

        Assert.Equal(TicketHistoryEntry.ValueMaxLength, entry.OldValue!.Length);
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            TicketHistoryEntry.Create(Guid.NewGuid(), TicketHistoryField.Status, "new", "open", null, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
}
