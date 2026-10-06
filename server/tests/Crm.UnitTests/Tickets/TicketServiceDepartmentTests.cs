using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.UnitTests.Departments;
using Crm.UnitTests.Sla;

namespace Crm.UnitTests.Tickets;

public class TicketServiceDepartmentTests
{
    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeDepartmentRepository _departments = new();
    private readonly FakeSlaPolicyRepository _sla;
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Guid _customerId;
    private readonly Department _billing;
    private readonly Department _support;

    public TicketServiceDepartmentTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _sla = new FakeSlaPolicyRepository(_clock.UtcNow.UtcDateTime);
        _customerId = _tickets.AddCustomer("Nour Trading");
        _billing = Department.Create("Billing", _clock.UtcNow.UtcDateTime);
        _support = Department.Create("Support", _clock.UtcNow.UtcDateTime);
        _departments.Add(_billing);
        _departments.Add(_support);
    }

    private TicketService Service(FakeDataScope? scope = null) =>
        new(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(Guid.NewGuid()), _clock,
            new CreateTicketRequestValidator(), new ListTicketsQueryValidator(), _sla, new FakeTicketHistoryRecorder(),
            new Crm.UnitTests.Settings.FakeSystemSettingsProvider(), null, _departments, scope ?? new FakeDataScope());

    private CreateTicketRequest Request(Guid? departmentId, string priority = "high") =>
        new(_customerId, "Invoice is wrong", null, null, priority, departmentId);

    [Fact]
    public async Task Create_WithADepartment_StoresIt()
    {
        var response = await Service().CreateAsync(Request(_billing.Id), CancellationToken.None);

        Assert.Equal(_billing.Id, Assert.Single(_tickets.Tickets).DepartmentId);
        Assert.Equal(_billing.Id, response.DepartmentId);
    }

    [Fact]
    public async Task Create_WithoutADepartment_LeavesItEmpty()
    {
        await Service().CreateAsync(Request(null), CancellationToken.None);

        Assert.Null(Assert.Single(_tickets.Tickets).DepartmentId);
    }

    [Fact]
    public async Task Create_WithAnUnknownOrInactiveDepartment_ThrowsValidationException_OnDepartmentId()
    {
        _support.Update("Support", false, _clock.UtcNow.UtcDateTime);

        var inactive = await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(Request(_support.Id), CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(Request(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(["departmentId"], inactive.Errors.Keys);
        Assert.Equal(["departmentId"], unknown.Errors.Keys);
        Assert.Empty(_tickets.Tickets);
    }

    [Fact]
    public async Task Create_ByARestrictedAgent_InAnotherDepartment_ThrowsValidationException()
    {
        var scope = new FakeDataScope(true, _billing.Id);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(scope).CreateAsync(Request(_support.Id), CancellationToken.None));

        Assert.Equal(["departmentId"], error.Errors.Keys);
    }

    [Fact]
    public async Task Create_ByARestrictedAgentWithOneDepartment_DefaultsToIt()
    {
        var scope = new FakeDataScope(true, _billing.Id);

        await Service(scope).CreateAsync(Request(null), CancellationToken.None);

        Assert.Equal(_billing.Id, Assert.Single(_tickets.Tickets).DepartmentId);
    }

    [Fact]
    public async Task Create_ByARestrictedAgentWithTwoDepartments_HasNoDefault()
    {
        var scope = new FakeDataScope(true, _billing.Id, _support.Id);

        await Service(scope).CreateAsync(Request(null), CancellationToken.None);

        Assert.Null(Assert.Single(_tickets.Tickets).DepartmentId);
    }

    [Fact]
    public async Task Create_UsesTheDepartmentSlaOverride_WhenOne_ElseTheGlobalPolicy()
    {
        _sla.DepartmentOverrides[(_billing.Id, TicketPriority.High)] = SlaPolicy.Create(TicketPriority.High, 30, 90, _clock.UtcNow.UtcDateTime);
        var now = _clock.UtcNow.UtcDateTime;

        await Service().CreateAsync(Request(_billing.Id), CancellationToken.None);
        await Service().CreateAsync(Request(_support.Id), CancellationToken.None);

        var withOverride = _tickets.Tickets.Single(t => t.DepartmentId == _billing.Id);
        var global = _tickets.Tickets.Single(t => t.DepartmentId == _support.Id);
        Assert.Equal(now.AddMinutes(30), withOverride.ResponseDueAt);
        Assert.Equal(now.AddMinutes(90), withOverride.ResolutionDueAt);
        Assert.Equal(now.AddMinutes(120), global.ResponseDueAt); // global High: 2 h
    }

    [Fact]
    public async Task ListQuery_WithADepartment_PassesItToTheFilter()
    {
        await Service().ListAsync(
            new ListTicketsQuery(null, null, null, null, null, null, null, null, null, null, _billing.Id), CancellationToken.None);

        Assert.Equal(_billing.Id, _tickets.LastFilter!.DepartmentId);
    }
}
