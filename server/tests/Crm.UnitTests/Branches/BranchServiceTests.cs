using Crm.Application.Branches;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Branches;
using Crm.UnitTests.Departments;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Branches;

internal sealed class FakeBranchRepository : IBranchRepository
{
    public List<Branch> Branches { get; } = [];

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Branch>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        IReadOnlyList<Branch> list = [.. Branches.Where(b => !activeOnly || b.IsActive).OrderBy(b => b.Name)];
        return Task.FromResult(list);
    }

    public Task<Branch?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Branches.FirstOrDefault(b => b.Id == id));

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(Branches.Any(b => b.NormalizedName == normalizedName && b.Id != exceptId));

    public void Add(Branch branch) => Branches.Add(branch);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

public class BranchServiceTests
{
    private readonly FakeBranchRepository _repository = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    private BranchService Service(FakeDataScope? scope = null) => new(_repository, _clock, new BranchRequestValidator(), scope);

    [Fact]
    public async Task Create_SavesAnActiveBranch_WithTheClockTime()
    {
        var response = await Service().CreateAsync(new BranchRequest(" Riyadh ", null), CancellationToken.None);

        var saved = Assert.Single(_repository.Branches);
        Assert.Equal((saved.Id, "Riyadh", true), (response.Id, response.Name, response.IsActive));
        Assert.Equal(_clock.UtcNow.UtcDateTime, response.CreatedAt);
    }

    [Fact]
    public async Task Create_WithADuplicateOrMissingName_ThrowsValidationException_OnName()
    {
        await Service().CreateAsync(new BranchRequest("Riyadh", null), CancellationToken.None);

        var duplicate = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new BranchRequest("  RIYADH ", null), CancellationToken.None));
        var missing = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new BranchRequest(" ", null), CancellationToken.None));

        Assert.Equal(["name"], duplicate.Errors.Keys);
        Assert.Equal(["name"], missing.Errors.Keys);
    }

    [Fact]
    public async Task Update_RenamesAndDeactivates_AndUnknownIsNotFound()
    {
        var created = await Service().CreateAsync(new BranchRequest("Riyadh", null), CancellationToken.None);

        var updated = await Service().UpdateAsync(created.Id, new BranchRequest("Jeddah", false), CancellationToken.None);

        Assert.Equal(("Jeddah", false), (updated.Name, updated.IsActive));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service().UpdateAsync(Guid.NewGuid(), new BranchRequest("X", true), CancellationToken.None));
    }

    [Fact]
    public async Task List_ActiveOnly_HidesInactive_AndABranchUserSeesOnlyTheirBranch()
    {
        var riyadh = await Service().CreateAsync(new BranchRequest("Riyadh", null), CancellationToken.None);
        await Service().CreateAsync(new BranchRequest("Jeddah", null), CancellationToken.None);
        await Service().CreateAsync(new BranchRequest("Old", false), CancellationToken.None);

        var everyone = await Service().ListAsync(new ListBranchesQuery(null), CancellationToken.None);
        var active = await Service().ListAsync(new ListBranchesQuery(true), CancellationToken.None);
        var own = await Service(new FakeDataScope { RestrictBranch = true, BranchId = riyadh.Id })
            .ListAsync(new ListBranchesQuery(null), CancellationToken.None);

        Assert.Equal(3, everyone.Count);
        Assert.Equal(["Jeddah", "Riyadh"], active.Select(b => b.Name));
        Assert.Equal([riyadh.Id], own.Select(b => b.Id));
    }
}
