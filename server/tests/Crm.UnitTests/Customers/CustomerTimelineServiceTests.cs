using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-10: reading a customer's timeline (type filter, paging, unknown customer).</summary>
public class CustomerTimelineServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly Customer _customer = Customer.Create("Nour", null, null, Now);
    private readonly FakeTimelineRepository _timeline = new();
    private readonly CustomerTimelineService _service;

    public CustomerTimelineServiceTests()
    {
        _service = new CustomerTimelineService(new OneCustomerRepository(_customer), _timeline, new CustomerTimelineQueryValidator());
    }

    [Fact]
    public async Task List_ReturnsTheRepositoryPage_WithDefaults()
    {
        var page = await _service.ListAsync(_customer.Id, new CustomerTimelineQuery(null, null, null), CancellationToken.None);

        Assert.Equal((_customer.Id, (InteractionType?)null, 1, 20), _timeline.LastList);
        Assert.Equal((1, 20), (page.Page, page.PageSize));
    }

    [Theory]
    [InlineData("customer", InteractionType.Customer)]
    [InlineData("note", InteractionType.Note)]
    [InlineData("attachment", InteractionType.Attachment)]
    [InlineData("ticket", InteractionType.Ticket)]
    [InlineData("message", InteractionType.Message)]
    public async Task List_WithType_PassesTheParsedType(string type, InteractionType expected)
    {
        await _service.ListAsync(_customer.Id, new CustomerTimelineQuery(type, 2, 10), CancellationToken.None);

        Assert.Equal((_customer.Id, (InteractionType?)expected, 2, 10), _timeline.LastList);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("Note")]
    [InlineData("1")]
    public async Task List_WithUnknownType_ThrowsValidationException_OnType(string type)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(_customer.Id, new CustomerTimelineQuery(type, null, null), CancellationToken.None));

        Assert.Equal(["type"], error.Errors.Keys);
        Assert.Null(_timeline.LastList);
    }

    [Fact]
    public async Task List_WithInvalidPaging_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(_customer.Id, new CustomerTimelineQuery(null, 0, 101), CancellationToken.None));

        Assert.Equal(["page", "pageSize"], error.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task List_OfUnknownCustomer_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ListAsync(Guid.NewGuid(), new CustomerTimelineQuery(null, null, null), CancellationToken.None));

        Assert.Null(_timeline.LastList);
    }
}
