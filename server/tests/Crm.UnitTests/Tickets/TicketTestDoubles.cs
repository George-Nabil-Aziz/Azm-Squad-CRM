using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Customers.Timeline;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

/// <summary>The signed-in user of a unit test.</summary>
internal sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => true;
}

/// <summary>Timeline entries a service recorded (the real recorder adds them to the unit of work).</summary>
internal sealed class FakeInteractionRecorder : IInteractionRecorder
{
    public List<(Guid CustomerId, InteractionType Type, string Event, string? Details, Guid? SourceId, DateTime UtcNow)> Entries { get; } = [];

    public void Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow) =>
        Entries.Add((customerId, type, @event, details, sourceId, utcNow));
}

/// <summary>
/// In-memory ticket storage with the same contract as the EF Core repository: added tickets are stored by
/// <see cref="SaveChangesAsync"/>, which rejects a duplicate number like the unique index; views show the customer /
/// category names (customers stay readable after deletion).
/// </summary>
internal sealed class FakeTicketRepository(FakeTicketCategoryRepository categories) : ITicketRepository
{
    private readonly List<Ticket> _pending = [];

    /// <summary>Customers that are not deleted (id → name): only these can get new tickets.</summary>
    public Dictionary<Guid, string> Customers { get; } = [];

    /// <summary>Every customer name, deleted ones included (what ticket views show).</summary>
    public Dictionary<Guid, string> AllCustomerNames { get; } = [];

    public List<Ticket> Tickets { get; } = [];

    public Guid AddCustomer(string name)
    {
        var id = Guid.NewGuid();
        Customers[id] = name;
        AllCustomerNames[id] = name;
        return id;
    }

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.ContainsKey(customerId));

    public int SaveCount { get; private set; }

    public async Task<int> NextNumberAsync(CancellationToken cancellationToken)
    {
        await Task.Yield(); // like a database round trip: lets concurrent callers interleave
        return Tickets.Select(t => t.Number).DefaultIfEmpty(0).Max() + 1;
    }

    public void Add(Ticket ticket) => _pending.Add(ticket);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (_pending.Any(p => Tickets.Any(t => t.Number == p.Number)))
        {
            throw new ConflictException("Duplicate ticket number.");
        }

        Tickets.AddRange(_pending);
        _pending.Clear();
        SaveCount++;
    }

    public TicketListFilter? LastFilter { get; private set; }

    public (int Page, int PageSize)? LastPaging { get; private set; }

    public List<TicketAssigneeResponse> Assignees { get; } = [];

    /// <summary>Records the filter; returns every saved ticket newest first (the real filtering is integration-tested).</summary>
    public Task<PagedResult<TicketView>> ListAsync(TicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        LastPaging = (page, pageSize);
        var all = Tickets.Where(t => filter.CustomerId is null || t.CustomerId == filter.CustomerId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number).Select(View).ToList();
        return Task.FromResult(new PagedResult<TicketView>([.. all.Skip((page - 1) * pageSize).Take(pageSize)], page, pageSize, all.Count));
    }

    public Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketAssigneeResponse>>(Assignees);

    public Task<Ticket?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.FirstOrDefault(t => t.Id == id));

    public Task<Ticket?> FindByNumberAsync(int number, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.FirstOrDefault(t => t.Number == number));

    public Task<Ticket?> FindLatestOpenAsync(Guid customerId, TicketChannel channel, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets
            .Where(t => t.CustomerId == customerId && t.Channel == channel && t.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number).FirstOrDefault());

    public Task<TicketView?> GetViewAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.FirstOrDefault(t => t.Id == id) is { } ticket ? View(ticket) : null);

    public TicketView View(Ticket ticket) => new(
        ticket,
        AllCustomerNames[ticket.CustomerId],
        categories.Categories.FirstOrDefault(c => c.Id == ticket.CategoryId)?.Name,
        Assignees.FirstOrDefault(a => a.Id == ticket.AssigneeId)?.FullName);

    public Task<TicketAssigneeResponse?> FindAssigneeAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Assignees.FirstOrDefault(a => a.Id == userId));
}

/// <summary>History entries a service recorded (the real recorder adds them to the unit of work).</summary>
internal sealed class FakeTicketHistoryRecorder : ITicketHistoryRecorder
{
    public List<(Guid TicketId, TicketHistoryField Field, string? OldValue, string? NewValue, DateTime UtcNow)> Entries { get; } = [];

    public void Record(Guid ticketId, TicketHistoryField field, string? oldValue, string? newValue, DateTime utcNow) =>
        Entries.Add((ticketId, field, oldValue, newValue, utcNow));
}

/// <summary>History storage with the contract of the EF Core repository: reads come back oldest first.</summary>
internal sealed class FakeTicketHistoryRepository : ITicketHistoryRepository
{
    public List<TicketHistoryItemResponse> Items { get; } = [];

    public void Add(TicketHistoryEntry entry)
    {
    }

    public Task<IReadOnlyList<TicketHistoryItemResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketHistoryItemResponse>>([.. Items.OrderBy(i => i.ChangedAt)]);
}

/// <summary>Test-only access to ticket state the Domain changes in later stories (status workflow, CRM-17).</summary>
internal static class TicketTestSupport
{
    public static void SetStatus(Ticket ticket, TicketStatus status) =>
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status);
}

/// <summary>Reply dispatches the service made (the real dispatcher is implemented by the channel stories).</summary>
internal sealed class FakeTicketReplyDispatcher : ITicketReplyDispatcher
{
    public List<TicketMessage> Dispatched { get; } = [];

    public List<string?> Templates { get; } = [];

    public Exception? ValidationError { get; set; }

    public int Validated { get; private set; }

    public Task ValidateAsync(Ticket ticket, string? templateName, CancellationToken cancellationToken)
    {
        Validated++;
        return ValidationError is null ? Task.CompletedTask : Task.FromException(ValidationError);
    }

    public Task DispatchAsync(TicketMessage message, string? templateName, CancellationToken cancellationToken)
    {
        Dispatched.Add(message);
        Templates.Add(templateName);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory message storage with the same contract as the EF Core repository (authors are named by <see cref="Authors"/>).</summary>
internal sealed class FakeTicketMessageRepository : ITicketMessageRepository
{
    public List<TicketMessage> Messages { get; } = [];

    public Dictionary<Guid, string> Authors { get; } = [];

    public void Add(TicketMessage message) => Messages.Add(message);

    public Task<TicketMessage?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));

    public Task<TicketMessageResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Messages.FirstOrDefault(m => m.Id == id) is { } message ? ToResponse(message) : null);

    public Task<IReadOnlyList<TicketMessageResponse>> ListAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketMessageResponse>>([.. Messages
            .Where(m => m.TicketId == ticketId && (includeInternal || !m.IsInternal))
            .OrderBy(m => m.CreatedAt).Select(ToResponse)]);

    private TicketMessageResponse ToResponse(TicketMessage message) => TicketMessageService.ToResponse(
        message, message.AuthorId is { } id && Authors.TryGetValue(id, out var name) ? name : null);
}

/// <summary>A clock the test sets by hand (the app uses TimeProvider.System).</summary>
internal sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>In-memory category storage with the same contract as the EF Core repository.</summary>
internal sealed class FakeTicketCategoryRepository : ITicketCategoryRepository
{
    public List<TicketCategory> Categories { get; } = [];

    public int SaveCount { get; private set; }

    public bool? LastActiveOnly { get; private set; }

    public Task<IReadOnlyList<TicketCategory>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        LastActiveOnly = activeOnly;
        IReadOnlyList<TicketCategory> list = [.. Categories.Where(c => !activeOnly || c.IsActive).OrderBy(c => c.Name)];
        return Task.FromResult(list);
    }

    public Task<TicketCategory?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.Any(c => c.NormalizedName == normalizedName && c.Id != exceptId));

    public void Add(TicketCategory category) => Categories.Add(category);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
