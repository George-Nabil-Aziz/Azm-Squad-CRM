using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Branches;
using Crm.Domain.Customers;
using Crm.UnitTests.Branches;
using Crm.UnitTests.Departments;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-62: the branch of a customer (create, update, scope) and the tickets that follow it.</summary>
public class CustomerServiceBranchTests
{
    private sealed class Repository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public List<(Guid CustomerId, Guid? BranchId)> Moves { get; } = [];

        public Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Customer>(Customers, page, pageSize, Customers.Count));

        public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Customers.SingleOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<Customer>> FindByContactAsync(
            IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Customer>>([]);

        public void Add(Customer customer) => Customers.Add(customer);

        public Task MoveTicketsToBranchAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken)
        {
            Moves.Add((customerId, branchId));
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private readonly Repository _customers = new();
    private readonly FakeBranchRepository _branches = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Branch _riyadh;
    private readonly Branch _jeddah;

    public CustomerServiceBranchTests()
    {
        _riyadh = Branch.Create("Riyadh", _clock.UtcNow.UtcDateTime);
        _jeddah = Branch.Create("Jeddah", _clock.UtcNow.UtcDateTime);
        _branches.Add(_riyadh);
        _branches.Add(_jeddah);
    }

    private CustomerService Service(FakeDataScope? scope = null) => new(
        _customers, _clock, new ListCustomersQueryValidator(), new CustomerRequestValidator(), new CustomerContactRequestValidator(),
        new CustomerLookupQueryValidator(), new FakeInteractionRecorder(), new Audit.FakeAuditLogger(), _branches, scope ?? new FakeDataScope());

    private FakeDataScope InBranch(Branch branch) => new() { RestrictBranch = true, BranchId = branch.Id };

    [Fact]
    public async Task Create_WithABranch_StoresIt_WhenUnrestricted()
    {
        var response = await Service().CreateAsync(new CustomerRequest("Nour", null, null, _riyadh.Id), CancellationToken.None);

        Assert.Equal(_riyadh.Id, response.BranchId);
        Assert.Equal(_riyadh.Id, Assert.Single(_customers.Customers).BranchId);
    }

    [Fact]
    public async Task Create_WithoutABranch_LeavesItEmpty_WhenUnrestricted()
    {
        var response = await Service().CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        Assert.Null(response.BranchId);
    }

    [Fact]
    public async Task Create_WithAnUnknownOrInactiveBranch_ThrowsValidationException_OnBranchId()
    {
        _jeddah.Update("Jeddah", false, _clock.UtcNow.UtcDateTime);

        var unknown = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new CustomerRequest("Nour", null, null, Guid.NewGuid()), CancellationToken.None));
        var inactive = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new CustomerRequest("Nour", null, null, _jeddah.Id), CancellationToken.None));

        Assert.Equal(["branchId"], unknown.Errors.Keys);
        Assert.Equal(["branchId"], inactive.Errors.Keys);
        Assert.Empty(_customers.Customers);
    }

    [Fact]
    public async Task Create_ByABranchUser_AlwaysGetsTheirOwnBranch()
    {
        var response = await Service(InBranch(_riyadh)).CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);
        var explicitOwn = await Service(InBranch(_riyadh)).CreateAsync(new CustomerRequest("Omar", null, null, _riyadh.Id), CancellationToken.None);

        Assert.Equal(_riyadh.Id, response.BranchId);
        Assert.Equal(_riyadh.Id, explicitOwn.BranchId);
    }

    [Fact]
    public async Task Create_ByABranchUser_NamingAnotherBranch_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(InBranch(_riyadh)).CreateAsync(new CustomerRequest("Nour", null, null, _jeddah.Id), CancellationToken.None));

        Assert.Equal(["branchId"], error.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithoutABranch_KeepsIt_AndNothingMoves()
    {
        var created = await Service().CreateAsync(new CustomerRequest("Nour", null, null, _riyadh.Id), CancellationToken.None);

        var updated = await Service().UpdateAsync(created.Id, new CustomerRequest("Nour Trading", null, null), CancellationToken.None);

        Assert.Equal(_riyadh.Id, updated.BranchId);
        Assert.Empty(_customers.Moves);
    }

    [Fact]
    public async Task Update_ToAnotherBranch_MovesTheCustomersTickets()
    {
        var created = await Service().CreateAsync(new CustomerRequest("Nour", null, null, _riyadh.Id), CancellationToken.None);

        var updated = await Service().UpdateAsync(created.Id, new CustomerRequest("Nour", null, null, _jeddah.Id), CancellationToken.None);

        Assert.Equal(_jeddah.Id, updated.BranchId);
        Assert.Equal([(created.Id, (Guid?)_jeddah.Id)], _customers.Moves);
    }

    [Fact]
    public async Task Update_ByABranchUser_CannotMoveTheCustomer()
    {
        var created = await Service().CreateAsync(new CustomerRequest("Nour", null, null, _riyadh.Id), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(InBranch(_riyadh)).UpdateAsync(created.Id, new CustomerRequest("Nour", null, null, _jeddah.Id), CancellationToken.None));
        var same = await Service(InBranch(_riyadh)).UpdateAsync(created.Id, new CustomerRequest("Nour", null, null, _riyadh.Id), CancellationToken.None);

        Assert.Equal(["branchId"], error.Errors.Keys);
        Assert.Equal(_riyadh.Id, same.BranchId);
        Assert.Empty(_customers.Moves);
    }
}
