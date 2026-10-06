using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public void Reply_IsOutbound_WithTrimmedBody_Author_AndUtcTime()
    {
        var message = TicketMessage.Staff(TicketId, "  We are on it.  ", isInternal: false, TicketChannel.Manual, AuthorId, Now);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(TicketId, message.TicketId);
        Assert.Equal(MessageDirection.Outbound, message.Direction);
        Assert.Equal("We are on it.", message.Body);
        Assert.Equal(AuthorId, message.AuthorId);
        Assert.Equal(Now, message.CreatedAt);
        Assert.False(message.IsInternal);
        Assert.Null(message.DeliveryStatus); // manual tickets are not delivered anywhere
        Assert.Null(message.ExternalMessageId);
    }

    [Fact]
    public void Note_IsInternal_AndNeverDelivered()
    {
        var message = TicketMessage.Staff(TicketId, "Customer is a VIP.", isInternal: true, TicketChannel.Email, AuthorId, Now);

        Assert.Equal(MessageDirection.InternalNote, message.Direction);
        Assert.True(message.IsInternal);
        Assert.Null(message.DeliveryStatus);
    }

    [Fact]
    public void Reply_OnAChannel_StartsPending()
    {
        var message = TicketMessage.Staff(TicketId, "Hello", isInternal: false, TicketChannel.WhatsApp, AuthorId, Now);

        Assert.Equal(MessageDeliveryStatus.Pending, message.DeliveryStatus);
    }

    [Fact]
    public void Inbound_HasNoAuthor_AndKeepsTheExternalId()
    {
        var message = TicketMessage.Inbound(TicketId, "Where is my order?", TicketChannel.Email, "<abc@mail>", Now);

        Assert.Equal(MessageDirection.Inbound, message.Direction);
        Assert.Null(message.AuthorId);
        Assert.Equal("<abc@mail>", message.ExternalMessageId);
        Assert.Null(message.DeliveryStatus);
    }

    [Fact]
    public void MarkSent_AndMarkFailed_UpdateTheDeliveryStatus()
    {
        var sent = TicketMessage.Staff(TicketId, "Hello", false, TicketChannel.Email, AuthorId, Now);
        sent.MarkSent("<id@mail>");
        var failed = TicketMessage.Staff(TicketId, "Hello", false, TicketChannel.Email, AuthorId, Now);
        failed.MarkFailed();

        Assert.Equal(MessageDeliveryStatus.Sent, sent.DeliveryStatus);
        Assert.Equal("<id@mail>", sent.ExternalMessageId);
        Assert.Equal(MessageDeliveryStatus.Failed, failed.DeliveryStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankBody_Throws(string body) =>
        Assert.ThrowsAny<ArgumentException>(() => TicketMessage.Staff(TicketId, body, false, TicketChannel.Manual, AuthorId, Now));

    [Fact]
    public void Create_WithTooLongBody_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            TicketMessage.Staff(TicketId, new string('x', TicketMessage.BodyMaxLength + 1), false, TicketChannel.Manual, AuthorId, Now));

    [Fact]
    public void Create_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            TicketMessage.Staff(TicketId, "Hello", false, TicketChannel.Manual, AuthorId, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
}
