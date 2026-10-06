using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

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
