using Crm.Application.Common.Security;
using Crm.Domain.Customers;

namespace Crm.Application.Customers.Timeline;

public sealed class InteractionRecorder(ICustomerTimelineRepository timeline, ICurrentUser currentUser) : IInteractionRecorder
{
    public void Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow) =>
        timeline.Add(CustomerInteraction.Create(customerId, type, @event, details, sourceId, currentUser.UserId, utcNow));
}
