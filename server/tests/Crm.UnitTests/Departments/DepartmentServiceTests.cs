using Crm.Application.Common.Exceptions;
using Crm.Application.Departments;
using Crm.Application.Sla;
using Crm.Domain.Audit;
using Crm.UnitTests.Audit;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Departments;

public class DepartmentServiceTests
{
    private readonly FakeDepartmentRepository _repository = new();
    private readonly FakeAuditLogger _audit = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly DepartmentService _service;

    public DepartmentServiceTests()
    {
        _service = new DepartmentService(_repository, _clock, new DepartmentRequestValidator(),
            new UpdateSlaPolicyRequestValidator(), _audit);
    }

    private Task<DepartmentResponse> CreateAsync(string name) =>
        _service.CreateAsync(new DepartmentRequest(name, null), CancellationToken.None);

    [Fact]
    public async Task Create_SavesAnActiveDepartment_WithTheClockTime()
    {
        var response = await CreateAsync(" Billing ");

        var saved = Assert.Single(_repository.Departments);
        Assert.Equal(saved.Id, response.Id);
        Assert.Equal("Billing", response.Name);
        Assert.True(response.IsActive);
        Assert.Equal(_clock.UtcNow.UtcDateTime, response.CreatedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_WithADuplicateName_ThrowsValidationException_OnName()
    {
        await CreateAsync("Billing");

        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync("  BILLING "));

        Assert.Equal(["name"], error.Errors.Keys);
        Assert.Single(_repository.Departments);
    }

    [Fact]
    public async Task Create_WithoutAName_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(" "));

        Assert.Equal(["name"], error.Errors.Keys);
    }

    [Fact]
    public async Task Update_RenamesAndDeactivates_AndUnknownIsNotFound()
    {
        var created = await CreateAsync("Billing");

        var updated = await _service.UpdateAsync(created.Id, new DepartmentRequest("Finance", false), CancellationToken.None);

        Assert.Equal("Finance", updated.Name);
        Assert.False(updated.IsActive);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), new DepartmentRequest("X", true), CancellationToken.None));
    }

    [Fact]
    public async Task List_ActiveOnly_HidesInactiveDepartments()
    {
        await CreateAsync("Billing");
        await _service.CreateAsync(new DepartmentRequest("Legacy", false), CancellationToken.None);

        var active = await _service.ListAsync(new ListDepartmentsQuery(true), CancellationToken.None);
        var all = await _service.ListAsync(new ListDepartmentsQuery(null), CancellationToken.None);

        Assert.Equal(["Billing"], active.Select(d => d.Name));
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task SetSlaPolicy_CreatesThenUpdatesTheOverride_AndAuditsIt()
    {
        var department = await CreateAsync("Billing");

        var first = await _service.SetSlaPolicyAsync(department.Id, "high", new UpdateSlaPolicyRequest(30, 120), CancellationToken.None);
        var second = await _service.SetSlaPolicyAsync(department.Id, "high", new UpdateSlaPolicyRequest(45, 200), CancellationToken.None);

        Assert.Equal(("high", 30, 120), (first.Priority, first.ResponseMinutes, first.ResolutionMinutes));
        Assert.Equal(45, second.ResponseMinutes);
        Assert.Single(_repository.SlaPolicies);
        Assert.All(_audit.Events, e => Assert.Equal(AuditActions.SlaPolicyUpdated, e.Action));
        Assert.Equal(2, _audit.Events.Count);
    }

    [Fact]
    public async Task SetSlaPolicy_WithInvalidMinutes_Throws_AndUnknownDepartmentOrPriorityIsNotFound()
    {
        var department = await CreateAsync("Billing");

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SetSlaPolicyAsync(department.Id, "high", new UpdateSlaPolicyRequest(100, 50), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SetSlaPolicyAsync(Guid.NewGuid(), "high", new UpdateSlaPolicyRequest(30, 60), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SetSlaPolicyAsync(department.Id, "urgent", new UpdateSlaPolicyRequest(30, 60), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveSlaPolicy_DeletesTheOverride_AndIsIdempotent()
    {
        var department = await CreateAsync("Billing");
        await _service.SetSlaPolicyAsync(department.Id, "low", new UpdateSlaPolicyRequest(30, 60), CancellationToken.None);

        await _service.RemoveSlaPolicyAsync(department.Id, "low", CancellationToken.None);
        await _service.RemoveSlaPolicyAsync(department.Id, "low", CancellationToken.None);

        Assert.Empty(_repository.SlaPolicies);
        Assert.Empty(await _service.ListSlaPoliciesAsync(department.Id, CancellationToken.None));
    }
}
