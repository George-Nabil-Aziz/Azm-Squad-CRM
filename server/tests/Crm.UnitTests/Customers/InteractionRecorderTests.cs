using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-10: the recorder every feature calls to add a timeline entry.</summary>
public class InteractionRecorderTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_AddsAnEntry_WithTheCurrentUserAsActor()
    {
        var repository = new FakeTimelineRepository();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var recorder = new InteractionRecorder(repository, new FakeCurrentUser(userId));

        recorder.Record(customerId, InteractionType.Ticket, "ticketCreated", "T-1", sourceId, Now);

        var entry = Assert.Single(repository.Added);
        Assert.Equal(
            (customerId, InteractionType.Ticket, "ticketCreated", "T-1", (Guid?)sourceId, (Guid?)userId, Now),
            (entry.CustomerId, entry.Type, entry.Event, entry.Details, entry.SourceId, entry.ActorId, entry.OccurredAt));
    }

    [Fact]
    public void Record_WithoutSignedInUser_HasNoActor()
    {
        var repository = new FakeTimelineRepository();
        var recorder = new InteractionRecorder(repository, new FakeCurrentUser(null));

        recorder.Record(Guid.NewGuid(), InteractionType.Message, "messageReceived", "Hello", null, Now);

        Assert.Null(Assert.Single(repository.Added).ActorId);
    }
}
