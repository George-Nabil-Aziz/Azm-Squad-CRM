using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

public class OutboundMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    private static OutboundMessage NewMessage() =>
        OutboundMessage.Create(ChannelKind.Email, " nour@example.com ", "Re: help [TKT-000001]", "Hello", null, Guid.NewGuid(), Now);

    [Fact]
    public void Create_StartsPending()
    {
        var message = NewMessage();

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(ChannelKind.Email, message.Channel);
        Assert.Equal("nour@example.com", message.Recipient);
        Assert.Equal(DeliveryStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.NextAttemptAt);
        Assert.Equal(Now, message.CreatedAt);
        Assert.Equal(Now, message.UpdatedAt);
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => OutboundMessage.Create(
            ChannelKind.Email, "a@b.c", null, "x", null, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)));

    [Fact]
    public void Create_WithoutRecipient_Throws() =>
        Assert.Throws<ArgumentException>(() => OutboundMessage.Create(ChannelKind.Email, " ", null, "x", null, null, Now));

    [Fact]
    public void MarkSent_SetsProviderIdAndSent()
    {
        var message = NewMessage();

        message.MarkSent("<abc@crm>", Now.AddSeconds(2));

        Assert.Equal(DeliveryStatus.Sent, message.Status);
        Assert.Equal("<abc@crm>", message.ProviderMessageId);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.NextAttemptAt);
        Assert.Null(message.LastError);
        Assert.Equal(Now.AddSeconds(2), message.UpdatedAt);
    }

    [Fact]
    public void MarkFailed_SchedulesTheNextAttempt_WithBackOff()
    {
        var message = NewMessage();
        int[] expectedMinutes = [1, 5, 15, 60];

        foreach (var minutes in expectedMinutes)
        {
            message.MarkFailed("SMTP down", Now);
            Assert.Equal(DeliveryStatus.Failed, message.Status);
            Assert.Equal("SMTP down", message.LastError);
            Assert.Equal(Now.AddMinutes(minutes), message.NextAttemptAt);
        }

        Assert.Equal(4, message.Attempts);
    }

    [Fact]
    public void MarkFailed_AfterTheLastAttempt_StopsRetrying()
    {
        var message = NewMessage();

        for (var i = 0; i < OutboundMessage.MaxAttempts; i++)
        {
            message.MarkFailed("SMTP down", Now);
        }

        Assert.Equal(OutboundMessage.MaxAttempts, message.Attempts);
        Assert.Equal(DeliveryStatus.Failed, message.Status);
        Assert.Null(message.NextAttemptAt);
        Assert.False(message.IsDueForRetry(Now.AddDays(1)));
    }

    [Fact]
    public void IsDueForRetry_OnlyWhenFailedAndTheTimeHasCome()
    {
        var message = NewMessage();
        Assert.False(message.IsDueForRetry(Now));

        message.MarkFailed("x", Now);

        Assert.False(message.IsDueForRetry(Now.AddSeconds(59)));
        Assert.True(message.IsDueForRetry(Now.AddMinutes(1)));
    }

    [Fact]
    public void ApplyDeliveryStatus_NeverGoesBackwards()
    {
        var message = NewMessage();
        message.MarkSent("wamid.1", Now);

        message.ApplyDeliveryStatus(DeliveryStatus.Read, null, Now.AddMinutes(2));
        message.ApplyDeliveryStatus(DeliveryStatus.Delivered, null, Now.AddMinutes(3)); // late webhook
        message.ApplyDeliveryStatus(DeliveryStatus.Sent, null, Now.AddMinutes(4));

        Assert.Equal(DeliveryStatus.Read, message.Status);
        Assert.Equal(Now.AddMinutes(2), message.UpdatedAt);
    }

    [Fact]
    public void ApplyDeliveryStatus_Failed_StoresTheError_AndIsNotRetried()
    {
        var message = NewMessage();
        message.MarkSent("wamid.1", Now);

        message.ApplyDeliveryStatus(DeliveryStatus.Failed, "Message undeliverable", Now.AddMinutes(1));

        Assert.Equal(DeliveryStatus.Failed, message.Status);
        Assert.Equal("Message undeliverable", message.LastError);
        Assert.Null(message.NextAttemptAt);
        Assert.False(message.IsDueForRetry(Now.AddDays(1)));
    }

    [Fact]
    public void MarkFailed_CutsLongErrors()
    {
        var message = NewMessage();

        message.MarkFailed(new string('e', 5000), Now);

        Assert.Equal(OutboundMessage.ErrorMaxLength, message.LastError!.Length);
    }
}
