using Crm.Application.Common.Exceptions;
using Crm.Application.Customers.Notes;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-11 AC 1: adding and listing notes.</summary>
public class CustomerNoteServiceTests
{
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Customer _customer = Customer.Create("Nour", null, null, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeNoteRepository _notes = new();
    private readonly FakeInteractionRecorder _timeline = new();
    private readonly CustomerNoteService _service;

    public CustomerNoteServiceTests()
    {
        _service = new CustomerNoteService(new OneCustomerRepository(_customer), _notes, _timeline, new FakeCurrentUser(_userId),
            _clock, new CustomerNoteRequestValidator(), new ListCustomerNotesQueryValidator());
    }

    [Fact]
    public async Task Add_SavesTheNote_WithTheCurrentUserAndTime_AndRecordsNoteAdded()
    {
        var note = await _service.AddAsync(_customer.Id, new CustomerNoteRequest("  Prefers WhatsApp.  "), CancellationToken.None);

        Assert.Equal(("Prefers WhatsApp.", (Guid?)_userId, $"User {_userId}", _clock.UtcNow.UtcDateTime),
            (note.Text, note.AuthorId, note.AuthorName, note.CreatedAt));
        Assert.Equal(1, _notes.SaveCount);
        Assert.Equal(
            [(_customer.Id, InteractionType.Note, InteractionEvents.NoteAdded, (string?)"Prefers WhatsApp.", (Guid?)note.Id, _clock.UtcNow.UtcDateTime)],
            _timeline.Entries);
    }

    [Fact]
    public async Task Add_WithEmptyText_ThrowsValidationException_AndSavesNothing()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddAsync(_customer.Id, new CustomerNoteRequest("   "), CancellationToken.None));

        Assert.Equal(["text"], error.Errors.Keys);
        Assert.Empty(_notes.Notes);
        Assert.Empty(_timeline.Entries);
    }

    [Fact]
    public async Task Add_ToUnknownCustomer_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddAsync(Guid.NewGuid(), new CustomerNoteRequest("Note"), CancellationToken.None));

        Assert.Empty(_notes.Notes);
    }

    [Fact]
    public async Task List_UsesDefaultPaging()
    {
        await _service.AddAsync(_customer.Id, new CustomerNoteRequest("Note"), CancellationToken.None);

        var page = await _service.ListAsync(_customer.Id, new ListCustomerNotesQuery(null, null), CancellationToken.None);

        Assert.Equal((1, 20, 1), (page.Page, page.PageSize, page.TotalCount));
    }
}
