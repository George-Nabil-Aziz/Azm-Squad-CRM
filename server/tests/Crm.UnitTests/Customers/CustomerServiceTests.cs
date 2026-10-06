using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly FakeCustomerRepository _repository = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _service = new CustomerService(_repository, _clock, new ListCustomersQueryValidator(), new CustomerRequestValidator());
    }

    [Fact]
    public async Task Create_WithAName_SavesTheCustomer_WithTheClockTime()
    {
        var response = await _service.CreateAsync(new CustomerRequest(" Nour ", "info@nour.example", null), CancellationToken.None);

        var saved = Assert.Single(_repository.Customers);
        Assert.Equal(saved.Id, response.Id);
        Assert.Equal("Nour", response.Name);
        Assert.Equal(_clock.UtcNow.UtcDateTime, response.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, response.CreatedAt.Kind);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_WithoutAName_ThrowsValidationException_WithTheNameField()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new CustomerRequest("  ", null, null), CancellationToken.None));

        Assert.Equal(["name"], error.Errors.Keys);
        Assert.Empty(_repository.Customers);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_ChangesTheProfile_AndUpdatedAt()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(10);

        var updated = await _service.UpdateAsync(created.Id, new CustomerRequest("Nour Trading", null, "0501234567"),
            CancellationToken.None);

        Assert.Equal("Nour Trading", updated.Name);
        Assert.Equal("0501234567", updated.Phone);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(_clock.UtcNow.UtcDateTime, updated.UpdatedAt);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task UpdateGetAndDelete_OfAnUnknownCustomer_ThrowNotFound()
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(id, new CustomerRequest("Nour", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_IsSoft_AndTheCustomerIsNoLongerFound()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);

        await _service.DeleteAsync(created.Id, CancellationToken.None);

        var row = Assert.Single(_repository.Customers); // still stored
        Assert.True(row.IsDeleted);
        Assert.Equal(_clock.UtcNow.UtcDateTime, row.DeletedAt);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task List_UsesDefaultPaging_AndATrimmedSearch()
    {
        await _service.ListAsync(new ListCustomersQuery("  nour  ", null, null), CancellationToken.None);
        Assert.Equal(("nour", 1, 20), _repository.LastList);

        await _service.ListAsync(new ListCustomersQuery("   ", 3, 50), CancellationToken.None);
        Assert.Equal((null, 3, 50), _repository.LastList);
    }

    [Fact]
    public async Task List_WithInvalidPaging_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new ListCustomersQuery(null, 0, 101), CancellationToken.None));

        Assert.Equal(["page", "pageSize"], error.Errors.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>In-memory repository: like the EF one, it never returns deleted customers.</summary>
    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public int SaveCount { get; private set; }

        public (string? Search, int Page, int PageSize)? LastList { get; private set; }

        public Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
        {
            LastList = (search, page, pageSize);
            List<Customer> visible = [.. Customers.Where(c => !c.IsDeleted)];
            return Task.FromResult(new PagedResult<Customer>(visible, page, pageSize, visible.Count));
        }

        public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Customers.SingleOrDefault(c => c.Id == id && !c.IsDeleted));

        public void Add(Customer customer) => Customers.Add(customer);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>A clock the test sets by hand (the app uses TimeProvider.System).</summary>
    private sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
