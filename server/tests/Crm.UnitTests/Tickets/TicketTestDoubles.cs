using Crm.Application.Common.Exceptions;
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

    public Task<TicketView?> GetViewAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.FirstOrDefault(t => t.Id == id) is { } ticket ? View(ticket) : null);

    public TicketView View(Ticket ticket) => new(
        ticket,
        AllCustomerNames[ticket.CustomerId],
        categories.Categories.FirstOrDefault(c => c.Id == ticket.CategoryId)?.Name,
        null);
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
