using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketCategoryServiceTests
{
    private readonly FakeTicketCategoryRepository _repository = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketCategoryService _service;

    public TicketCategoryServiceTests()
    {
        _service = new TicketCategoryService(_repository, _clock, new TicketCategoryRequestValidator());
    }

    private Task<TicketCategoryResponse> CreateAsync(string name) =>
        _service.CreateAsync(new TicketCategoryRequest(name, null), CancellationToken.None);

    [Fact]
    public async Task Create_SavesAnActiveCategory_WithTheClockTime()
    {
        var response = await CreateAsync(" Billing ");

        var saved = Assert.Single(_repository.Categories);
        Assert.Equal(saved.Id, response.Id);
        Assert.Equal("Billing", response.Name);
        Assert.True(response.IsActive);
        Assert.Equal(_clock.UtcNow.UtcDateTime, response.CreatedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_InactiveWhenAskedFor()
    {
        var response = await _service.CreateAsync(new TicketCategoryRequest("Legacy", false), CancellationToken.None);

        Assert.False(response.IsActive);
    }

    [Fact]
    public async Task Create_WithADuplicateName_IgnoringCaseAndSpaces_ThrowsValidationException_OnName()
    {
        await CreateAsync("Billing");

        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync("  BILLING "));

        Assert.Equal(["name"], error.Errors.Keys);
        Assert.Single(_repository.Categories);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_WithoutAName_ThrowsValidationException(string? name)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new TicketCategoryRequest(name, null), CancellationToken.None));

        Assert.Equal(["name"], error.Errors.Keys);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_ToTheNameOfAnotherCategory_ThrowsValidationException()
    {
        await CreateAsync("Billing");
        var other = await CreateAsync("Delivery");

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(other.Id, new TicketCategoryRequest("billing", null), CancellationToken.None));

        Assert.Equal(["name"], error.Errors.Keys);
    }

    [Fact]
    public async Task Update_KeepingItsOwnNameInAnotherCase_IsAllowed()
    {
        var created = await CreateAsync("Billing");

        var updated = await _service.UpdateAsync(created.Id, new TicketCategoryRequest("BILLING", null), CancellationToken.None);

        Assert.Equal("BILLING", updated.Name);
    }

    [Fact]
    public async Task Update_Deactivates_WhenIsActiveFalse_AndKeepsTheStateWhenNull()
    {
        var created = await CreateAsync("Billing");
        _clock.UtcNow = _clock.UtcNow.AddMinutes(3);

        var deactivated = await _service.UpdateAsync(created.Id, new TicketCategoryRequest("Billing", false), CancellationToken.None);
        var renamed = await _service.UpdateAsync(created.Id, new TicketCategoryRequest("Invoices", null), CancellationToken.None);

        Assert.False(deactivated.IsActive);
        Assert.Equal(_clock.UtcNow.UtcDateTime, deactivated.UpdatedAt);
        Assert.False(renamed.IsActive);
        Assert.Equal("Invoices", renamed.Name);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), new TicketCategoryRequest("Billing", null), CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task List_PassesActiveOnly_ToTheRepository(bool activeOnly)
    {
        await CreateAsync("Billing");
        await _service.CreateAsync(new TicketCategoryRequest("Legacy", false), CancellationToken.None);

        var list = await _service.ListAsync(new ListTicketCategoriesQuery(activeOnly), CancellationToken.None);

        Assert.Equal(activeOnly, _repository.LastActiveOnly);
        Assert.Equal(activeOnly ? ["Billing"] : ["Billing", "Legacy"], list.Select(c => c.Name));
    }

    [Fact]
    public async Task List_WithoutActiveOnly_ReturnsEveryCategory()
    {
        await _service.ListAsync(new ListTicketCategoriesQuery(null), CancellationToken.None);

        Assert.False(_repository.LastActiveOnly);
    }
}
