using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-10: one entry of a customer's timeline.</summary>
public class CustomerInteractionTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsEveryField()
    {
        var customerId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var entry = CustomerInteraction.Create(
            customerId, InteractionType.Ticket, "ticketCreated", "T-1 Printer broken", sourceId, actorId, Now);

        Assert.Equal(customerId, entry.CustomerId);
        Assert.Equal(InteractionType.Ticket, entry.Type);
        Assert.Equal("ticketCreated", entry.Event);
        Assert.Equal("T-1 Printer broken", entry.Details);
        Assert.Equal(sourceId, entry.SourceId);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(Now, entry.OccurredAt);
    }

    [Theory]
    [InlineData("  Nour Trading  ", "Nour Trading")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Create_TrimsDetails_AndEmptyBecomesNull(string? details, string? expected)
    {
        var entry = CustomerInteraction.Create(
            Guid.NewGuid(), InteractionType.Customer, InteractionEvents.CustomerCreated, details, null, null, Now);

        Assert.Equal(expected, entry.Details);
    }

    [Fact]
    public void Create_CutsLongDetails_To500Characters()
    {
        var entry = CustomerInteraction.Create(
            Guid.NewGuid(), InteractionType.Note, "noteAdded", new string('x', 600), null, null, Now);

        Assert.Equal(CustomerInteraction.DetailsMaxLength, entry.Details!.Length);
        Assert.EndsWith("…", entry.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithoutEvent_Throws(string @event) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            CustomerInteraction.Create(Guid.NewGuid(), InteractionType.Customer, @event, null, null, null, Now));

    [Fact]
    public void Create_WithEmptyCustomerId_Throws() =>
        Assert.Throws<ArgumentException>(() => CustomerInteraction.Create(
            Guid.Empty, InteractionType.Customer, InteractionEvents.CustomerCreated, null, null, null, Now));

    [Fact]
    public void Create_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => CustomerInteraction.Create(
            Guid.NewGuid(), InteractionType.Customer, InteractionEvents.CustomerCreated, null, null, null,
            DateTime.SpecifyKind(Now, DateTimeKind.Local)));
}
