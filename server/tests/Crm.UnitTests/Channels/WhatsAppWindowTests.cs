using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

public class WhatsAppWindowTests
{
    private static readonly DateTime LastMessage = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsOpen_JustBefore24Hours() =>
        Assert.True(WhatsAppWindow.IsOpen(LastMessage, LastMessage.AddHours(24).AddSeconds(-1)));

    [Fact]
    public void IsClosed_AtExactly24Hours() =>
        Assert.False(WhatsAppWindow.IsOpen(LastMessage, LastMessage.AddHours(24)));

    [Fact]
    public void IsClosed_Later() =>
        Assert.False(WhatsAppWindow.IsOpen(LastMessage, LastMessage.AddDays(3)));

    [Fact]
    public void IsClosed_WithoutAnyCustomerMessage() =>
        Assert.False(WhatsAppWindow.IsOpen(null, LastMessage));

    [Fact]
    public void IsOpen_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => WhatsAppWindow.IsOpen(LastMessage, DateTime.Now));
}
