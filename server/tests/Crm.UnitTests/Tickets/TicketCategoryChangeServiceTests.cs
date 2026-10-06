using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketCategoryChangeServiceTests
{
    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeTicketHistoryRecorder _history = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketCategoryChangeService _service;
    private readonly Ticket _ticket;
    private readonly TicketCategory _billing;
    private readonly TicketCategory _support;

    public TicketCategoryChangeServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketCategoryChangeService(_tickets, _categories, _history, _clock);
        var now = _clock.UtcNow.UtcDateTime;
        _billing = TicketCategory.Create("Billing", now);
        _support = TicketCategory.Create("Support", now);
        _categories.Add(_billing);
        _categories.Add(_support);
        _ticket = Ticket.Create(_tickets.AddCustomer("Nour"), "Invoice is wrong", null, _billing.Id, TicketPriority.High, TicketChannel.Manual, null, now);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
    }

    private Task<TicketResponse> ChangeAsync(Guid? categoryId) =>
        _service.ChangeAsync(_ticket.Id, new ChangeTicketCategoryRequest(categoryId), CancellationToken.None);

    [Fact]
    public async Task Change_UpdatesTheCategory_AndRecordsOldAndNewName()
    {
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);

        var response = await ChangeAsync(_support.Id);

        Assert.Equal(_support.Id, response.CategoryId);
        var entry = Assert.Single(_history.Entries);
        Assert.Equal((_ticket.Id, TicketHistoryField.Category, "Billing", "Support", _clock.UtcNow.UtcDateTime),
            (entry.TicketId, entry.Field, entry.OldValue, entry.NewValue, entry.UtcNow));
    }

    [Fact]
    public async Task Change_ToNone_RecordsANullNewValue()
    {
        var response = await ChangeAsync(null);

        Assert.Null(response.CategoryId);
        Assert.Equal(("Billing", (string?)null), (_history.Entries[0].OldValue, _history.Entries[0].NewValue));
    }

    [Fact]
    public async Task Change_ToTheSameCategory_RecordsNothing()
    {
        await ChangeAsync(_billing.Id);

        Assert.Empty(_history.Entries);
        Assert.Equal(0, _tickets.SaveCount);
    }

    [Fact]
    public async Task Change_ToAnInactiveOrUnknownCategory_ThrowsValidationException_OnCategoryId()
    {
        _support.Update("Support", false, _clock.UtcNow.UtcDateTime);

        var inactive = await Assert.ThrowsAsync<ValidationException>(() => ChangeAsync(_support.Id));
        var unknown = await Assert.ThrowsAsync<ValidationException>(() => ChangeAsync(Guid.NewGuid()));

        Assert.Contains("categoryId", inactive.Errors.Keys);
        Assert.Contains("categoryId", unknown.Errors.Keys);
        Assert.Equal(_billing.Id, _ticket.CategoryId);
        Assert.Empty(_history.Entries);
    }

    [Fact]
    public async Task UnknownTicket_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeAsync(Guid.NewGuid(), new ChangeTicketCategoryRequest(null), CancellationToken.None));
}
