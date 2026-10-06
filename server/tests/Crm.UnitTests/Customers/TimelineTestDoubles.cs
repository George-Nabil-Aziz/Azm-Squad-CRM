using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>Records every <see cref="IInteractionRecorder.Record"/> call.</summary>
internal sealed class FakeInteractionRecorder : IInteractionRecorder
{
    public List<(Guid CustomerId, InteractionType Type, string Event, string? Details, Guid? SourceId, DateTime UtcNow)> Entries { get; } = [];

    public void Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow) =>
        Entries.Add((customerId, type, @event, details, sourceId, utcNow));
}

/// <summary>Timeline storage in memory: keeps added entries and answers the last list call with a fixed page.</summary>
internal sealed class FakeTimelineRepository : ICustomerTimelineRepository
{
    public List<CustomerInteraction> Added { get; } = [];

    public (Guid CustomerId, InteractionType? Type, int Page, int PageSize)? LastList { get; private set; }

    public void Add(CustomerInteraction interaction) => Added.Add(interaction);

    public Task<PagedResult<CustomerInteractionResponse>> ListAsync(
        Guid customerId, InteractionType? type, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (customerId, type, page, pageSize);
        return Task.FromResult(new PagedResult<CustomerInteractionResponse>([], page, pageSize, 0));
    }
}

/// <summary>The signed-in user of a unit test (null = anonymous / system).</summary>
internal sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => false;
}
