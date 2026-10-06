using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Departments;
using Crm.Domain.Tickets;
using Crm.UnitTests.Departments;

namespace Crm.UnitTests.Tickets;

public class TicketDepartmentServiceTests
{
    private readonly FakeDepartmentRepository _departments = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeTicketHistoryRecorder _history = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketDepartmentService _service;
    private readonly Department _billing;
    private readonly Department _support;
    private readonly Ticket _ticket;

    public TicketDepartmentServiceTests()
    {
        _tickets = new FakeTicketRepository(new FakeTicketCategoryRepository());
        _service = new TicketDepartmentService(_tickets, _departments, _history, _clock);
        _billing = Department.Create("Billing", _clock.UtcNow.UtcDateTime);
        _support = Department.Create("Support", _clock.UtcNow.UtcDateTime);
        _departments.Add(_billing);
        _departments.Add(_support);
        var customerId = _tickets.AddCustomer("Nour");
        _ticket = Ticket.Create(customerId, "Subject", null, null, TicketPriority.Mid, TicketChannel.Manual, null, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _ticket.ChangeDepartment(_billing.Id, _clock.UtcNow.UtcDateTime);
        _tickets.Tickets.Add(_ticket);
    }

    [Fact]
    public async Task Transfer_ChangesTheDepartment_AndRecordsOldAndNewNames()
    {
        var response = await _service.TransferAsync(_ticket.Id, new TransferTicketRequest(_support.Id), CancellationToken.None);

        Assert.Equal(_support.Id, _ticket.DepartmentId);
        Assert.Equal(_support.Id, response.DepartmentId);
        Assert.Equal("Support", response.DepartmentName);
        var entry = Assert.Single(_history.Entries);
        Assert.Equal((_ticket.Id, TicketHistoryField.Department, "Billing", "Support"), (entry.TicketId, entry.Field, entry.OldValue, entry.NewValue));
        Assert.Equal(1, _tickets.SaveCount);
    }

    [Fact]
    public async Task Transfer_ToTheSameDepartment_RecordsNothing()
    {
        await _service.TransferAsync(_ticket.Id, new TransferTicketRequest(_billing.Id), CancellationToken.None);

        Assert.Empty(_history.Entries);
        Assert.Equal(0, _tickets.SaveCount);
    }

    [Fact]
    public async Task Transfer_ToNull_MovesTheTicketToGeneral()
    {
        var response = await _service.TransferAsync(_ticket.Id, new TransferTicketRequest(null), CancellationToken.None);

        Assert.Null(_ticket.DepartmentId);
        Assert.Null(response.DepartmentName);
        Assert.Equal("Billing", Assert.Single(_history.Entries).OldValue);
        Assert.Null(_history.Entries[0].NewValue);
    }

    [Fact]
    public async Task Transfer_ToAnUnknownOrInactiveDepartment_ThrowsValidationException_OnDepartmentId()
    {
        _support.Update("Support", false, _clock.UtcNow.UtcDateTime);

        var inactive = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.TransferAsync(_ticket.Id, new TransferTicketRequest(_support.Id), CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.TransferAsync(_ticket.Id, new TransferTicketRequest(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(["departmentId"], inactive.Errors.Keys);
        Assert.Equal(["departmentId"], unknown.Errors.Keys);
        Assert.Equal(_billing.Id, _ticket.DepartmentId);
    }

    [Fact]
    public async Task Transfer_OfAnUnknownTicket_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.TransferAsync(Guid.NewGuid(), new TransferTicketRequest(_support.Id), CancellationToken.None));
}
