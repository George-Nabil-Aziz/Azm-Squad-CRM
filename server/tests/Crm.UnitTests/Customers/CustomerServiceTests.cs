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
        _service = new CustomerService(_repository, _clock, new ListCustomersQueryValidator(), new CustomerRequestValidator(),
            new CustomerContactRequestValidator(), new CustomerLookupQueryValidator());
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
        Assert.Equal("+966501234567", updated.Phone); // stored in E.164 (CRM-9)
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

    [Fact]
    public async Task Create_ReturnsThePrimaryContacts_WithTheNormalizedPhone()
    {
        var response = await _service.CreateAsync(new CustomerRequest("Nour", "Info@Nour.Example", "050 123 4567"),
            CancellationToken.None);

        Assert.Equal("+966501234567", response.Phone);
        Assert.Equal("info@nour.example", response.Email);
        Assert.Equal(
            [("phone", "+966501234567", true), ("email", "info@nour.example", true)],
            response.Contacts.Select(c => (c.Type, c.Value, c.IsPrimary)));
    }

    [Fact]
    public async Task AddContact_NormalizesThePhoneToE164_AndSaves()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        var contact = await _service.AddContactAsync(created.Id, new CustomerContactRequest("whatsapp", "٠٥٠١٢٣٤٥٦٧", null),
            CancellationToken.None);

        Assert.Equal(("whatsapp", "+966501234567", true), (contact.Type, contact.Value, contact.IsPrimary));
        Assert.Equal(contact.Id, Assert.Single(_repository.Customers.Single().Contacts).Id);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task AddContact_WithInvalidPhone_ThrowsValidationException_AndSavesNothing()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddContactAsync(created.Id, new CustomerContactRequest("phone", "12345", null), CancellationToken.None));

        Assert.Equal(["value"], error.Errors.Keys);
        Assert.Empty(_repository.Customers.Single().Contacts);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task AddContact_ThatTheCustomerAlreadyHas_ThrowsConflict()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, "+966501234567"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.AddContactAsync(created.Id, new CustomerContactRequest("phone", "0501234567", null), CancellationToken.None));
    }

    [Fact]
    public async Task MakeContactPrimary_UnsetsTheOldPrimary()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, "+966501234567"), CancellationToken.None);
        var second = await _service.AddContactAsync(created.Id, new CustomerContactRequest("phone", "+966551234567", null),
            CancellationToken.None);

        await _service.MakeContactPrimaryAsync(created.Id, second.Id, CancellationToken.None);

        var customer = await _service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal("+966551234567", customer.Phone);
        Assert.Equal(["+966551234567"], customer.Contacts.Where(c => c.IsPrimary).Select(c => c.Value));
    }

    [Fact]
    public async Task ContactChanges_OfAnUnknownCustomerOrContact_ThrowNotFound()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddContactAsync(
            Guid.NewGuid(), new CustomerContactRequest("phone", "+966501234567", null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.MakeContactPrimaryAsync(created.Id, Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RemoveContactAsync(created.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_ByPhone_SearchesPhoneAndWhatsApp_WithTheE164Number()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, "+966501234567"), CancellationToken.None);

        var found = await _service.LookupAsync(new CustomerLookupQuery("050 123 4567", null), CancellationToken.None);

        Assert.Equal(created.Id, Assert.Single(found).Id);
        Assert.Equal([ContactType.Phone, ContactType.WhatsApp], _repository.LastLookup?.Types);
        Assert.Equal("+966501234567", _repository.LastLookup?.Value);
    }

    [Fact]
    public async Task Lookup_ByEmail_SearchesEmails_InLowerCase()
    {
        await _service.LookupAsync(new CustomerLookupQuery(null, " Info@Nour.Example "), CancellationToken.None);

        Assert.Equal([ContactType.Email], _repository.LastLookup?.Types);
        Assert.Equal("info@nour.example", _repository.LastLookup?.Value);
    }

    [Fact]
    public async Task Lookup_WithoutPhoneOrEmail_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.LookupAsync(new CustomerLookupQuery(null, null), CancellationToken.None));

        Assert.Equal(["phone"], error.Errors.Keys);
        Assert.Null(_repository.LastLookup);
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

        public (ContactType[] Types, string Value)? LastLookup { get; private set; }

        public Task<IReadOnlyList<Customer>> FindByContactAsync(
            IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken)
        {
            LastLookup = ([.. types], value);
            IReadOnlyList<Customer> found = [.. Customers.Where(c => !c.IsDeleted
                && c.Contacts.Any(x => types.Contains(x.Type) && x.Value == value))];
            return Task.FromResult(found);
        }

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
