using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Integrations;
using Crm.Domain.Customers;
using Crm.Domain.Integrations;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.UnitTests.Integrations;

public class CustomerErpLinkTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LinkErp_TrimsTheId_AndBlankRemovesTheLink()
    {
        var customer = Customer.Create("Nour Trading", null, null, Now);

        customer.LinkErp("  ERP-42 ", Now);
        Assert.Equal("ERP-42", customer.ErpCustomerId);

        customer.LinkErp("   ", Now.AddMinutes(1));
        Assert.Null(customer.ErpCustomerId);
    }

    [Fact]
    public void LinkErp_RejectsATooLongId_AndDeletedCustomers()
    {
        var customer = Customer.Create("Nour Trading", null, null, Now);

        Assert.Throws<ArgumentException>(() => customer.LinkErp(new string('x', Customer.ErpCustomerIdMaxLength + 1), Now));
        customer.Delete(Now);
        Assert.ThrowsAny<Exception>(() => customer.LinkErp("ERP-1", Now));
    }
}

public class ErpServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeErpRepository _repo = new();
    private readonly FakeErpClient _client = new();
    private readonly IntegrationClock _time = new(Start);

    private ErpService Service() => new(_repo, _client, _time);

    private Customer AddCustomer(string? erpId = null)
    {
        var customer = Customer.Create("Nour Trading", null, null, Start.UtcDateTime);
        customer.LinkErp(erpId, Start.UtcDateTime);
        _repo.Customers.Add(customer);
        return customer;
    }

    [Fact]
    public async Task Link_SetsTheErpId_AndUnlinkClearsIt()
    {
        var customer = AddCustomer();

        var linked = await Service().LinkAsync(customer.Id, new ErpLinkRequest(" ERP-9 "), default);
        Assert.Equal("ERP-9", linked.ErpCustomerId);
        Assert.Equal("ERP-9", customer.ErpCustomerId);

        var unlinked = await Service().LinkAsync(customer.Id, new ErpLinkRequest(null), default);
        Assert.Null(unlinked.ErpCustomerId);
    }

    [Fact]
    public async Task Link_AnIdOfAnotherCustomer_IsAConflict()
    {
        AddCustomer("ERP-1");
        var other = AddCustomer();

        await Assert.ThrowsAsync<ConflictException>(() => Service().LinkAsync(other.Id, new ErpLinkRequest("ERP-1"), default));

        Assert.Null(other.ErpCustomerId);
    }

    [Fact]
    public async Task Link_RelinkingTheSameIdOfTheSameCustomer_IsFine()
    {
        var customer = AddCustomer("ERP-1");

        await Service().LinkAsync(customer.Id, new ErpLinkRequest("ERP-1"), default);
    }

    [Fact]
    public async Task Link_TooLongOrUnknownCustomer_AreRejected()
    {
        var customer = AddCustomer();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().LinkAsync(customer.Id, new ErpLinkRequest(new string('x', 101)), default));
        Assert.Contains("erpCustomerId", error.Errors.Keys);
        await Assert.ThrowsAsync<NotFoundException>(() => Service().LinkAsync(Guid.NewGuid(), new ErpLinkRequest("ERP-1"), default));
    }

    [Fact]
    public async Task Get_ForAnUnlinkedCustomer_SaysSo_WithoutCallingTheErp()
    {
        var customer = AddCustomer();

        var response = await Service().GetAsync(customer.Id, default);

        Assert.False(response.Linked);
        Assert.Empty(_client.Calls);
        Assert.Empty(_repo.Logs);
    }

    [Fact]
    public async Task Get_ReturnsTheRecentOrdersAndInvoices_NewestFirst_AndLogsTheSync()
    {
        var customer = AddCustomer("ERP-7");
        _client.Data = new ErpCustomerData(
            [.. Enumerable.Range(1, 7).Select(i => new ErpOrder($"o{i}", $"SO-{i}", new DateTime(2026, 9, i, 0, 0, 0, DateTimeKind.Utc), "shipped", i * 10m, "SAR"))],
            [new ErpInvoice("i1", "INV-1", new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), null, "paid", 99m, "SAR")]);

        var response = await Service().GetAsync(customer.Id, default);

        Assert.True(response.Linked);
        Assert.True(response.Available);
        Assert.Null(response.Message);
        Assert.Equal(["o7", "o6", "o5", "o4", "o3"], response.Orders.Select(o => o.Id));
        Assert.Equal("INV-1", Assert.Single(response.Invoices).Number);
        Assert.Equal(("ERP-7", 5), (_client.Calls[0].Id, _client.Calls[0].Limit));
        var log = Assert.Single(_repo.Logs);
        Assert.Equal((customer.Id, "ERP-7", ErpSyncResult.Success, Start.UtcDateTime), (log.CustomerId, log.ErpCustomerId, log.Result, log.CreatedAt));
    }

    [Fact]
    public async Task Get_WhenTheErpIsDown_StillAnswers_WithAMessage_AndLogsTheFailure()
    {
        var customer = AddCustomer("ERP-7");
        _client.Failure = new ErpUnavailableException("The ERP answered 503");

        var response = await Service().GetAsync(customer.Id, default);

        Assert.True(response.Linked);
        Assert.False(response.Available);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
        Assert.Empty(response.Orders);
        Assert.Empty(response.Invoices);
        var log = Assert.Single(_repo.Logs);
        Assert.Equal(ErpSyncResult.Failed, log.Result);
        Assert.Equal("The ERP answered 503", log.Error);
    }

    [Fact]
    public async Task Get_WhenTheErpIsNotConfigured_SaysSo_AndLogsIt()
    {
        var customer = AddCustomer("ERP-7");
        _client.Failure = new ErpNotConfiguredException();

        var response = await Service().GetAsync(customer.Id, default);

        Assert.False(response.Available);
        Assert.Contains("not configured", response.Message);
        Assert.Equal(ErpSyncResult.NotConfigured, Assert.Single(_repo.Logs).Result);
    }

    [Fact]
    public async Task Get_UnknownCustomer_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync(Guid.NewGuid(), default));
}

internal sealed class FakeErpClient : IErpClient
{
    public List<(string Id, int Limit)> Calls { get; } = [];

    public ErpCustomerData Data { get; set; } = new([], []);

    public Exception? Failure { get; set; }

    public Task<ErpCustomerData> GetCustomerDataAsync(string erpCustomerId, int limit, CancellationToken cancellationToken)
    {
        Calls.Add((erpCustomerId, limit));
        return Failure is null ? Task.FromResult(Data) : Task.FromException<ErpCustomerData>(Failure);
    }
}

internal sealed class FakeErpRepository : IErpRepository
{
    public List<Customer> Customers { get; } = [];

    public List<ErpSyncLog> Logs { get; } = [];

    public Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.FirstOrDefault(c => c.Id == customerId));

    public Task<bool> ErpIdTakenAsync(string erpCustomerId, Guid exceptCustomerId, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.Any(c => c.Id != exceptCustomerId && c.ErpCustomerId == erpCustomerId));

    public void AddLog(ErpSyncLog log) => Logs.Add(log);

    public Task<PagedResult<ErpSyncLogResponse>> ListLogsAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResult<ErpSyncLogResponse>([], page, pageSize, 0));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
