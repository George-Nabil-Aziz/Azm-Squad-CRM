using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers.Notes;

public sealed class CustomerNoteService(
    ICustomerRepository customers,
    ICustomerNoteRepository notes,
    IInteractionRecorder timeline,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CustomerNoteRequest> requestValidator,
    IValidator<ListCustomerNotesQuery> listValidator) : ICustomerNoteService
{
    public async Task<PagedResult<CustomerNoteResponse>> ListAsync(
        Guid customerId, ListCustomerNotesQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        await EnsureCustomerAsync(customerId, cancellationToken);

        return await notes.ListAsync(
            customerId,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);
    }

    public async Task<CustomerNoteResponse> AddAsync(Guid customerId, CustomerNoteRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        await EnsureCustomerAsync(customerId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var note = CustomerNote.Create(customerId, request.Text!, currentUser.UserId, now);
        notes.Add(note);
        timeline.Record(customerId, InteractionType.Note, InteractionEvents.NoteAdded, note.Text, note.Id, now);
        await notes.SaveChangesAsync(cancellationToken);

        return await notes.GetAsync(note.Id, cancellationToken)
               ?? throw new InvalidOperationException("The saved note was not found.");
    }

    private async Task EnsureCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        _ = await customers.FindAsync(customerId, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);
}
