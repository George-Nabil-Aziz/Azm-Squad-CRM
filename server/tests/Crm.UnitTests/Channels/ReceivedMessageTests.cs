using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

public class ReceivedMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsEveryField_AndCutsLongBodies()
    {
        var customerId = Guid.NewGuid();

        var message = ReceivedMessage.Create(
            ChannelKind.Email, " <abc@mail.example> ", " nour@example.com ", " Nour ", " Help [TKT-000004] ",
            new string('x', ReceivedMessage.BodyMaxLength + 50), 4, customerId, Now.AddMinutes(-1), Now);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(ChannelKind.Email, message.Channel);
        Assert.Equal("<abc@mail.example>", message.ExternalId);
        Assert.Equal("nour@example.com", message.From);
        Assert.Equal("Nour", message.FromName);
        Assert.Equal("Help [TKT-000004]", message.Subject);
        Assert.Equal(ReceivedMessage.BodyMaxLength, message.Body.Length);
        Assert.EndsWith("…", message.Body, StringComparison.Ordinal);
        Assert.Equal(4, message.TicketNumber);
        Assert.Equal(customerId, message.CustomerId);
        Assert.Equal(Now.AddMinutes(-1), message.ReceivedAt);
        Assert.Equal(Now, message.CreatedAt);
    }

    [Fact]
    public void Create_WithoutExternalId_Throws() =>
        Assert.Throws<ArgumentException>(() => ReceivedMessage.Create(
            ChannelKind.Email, " ", "a@b.c", null, null, "x", null, null, Now, Now));

    [Fact]
    public void Create_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => ReceivedMessage.Create(
            ChannelKind.Email, "id", "a@b.c", null, null, "x", null, null, DateTime.Now, Now));
}
