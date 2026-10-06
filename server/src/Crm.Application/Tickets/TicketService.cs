using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>
/// Ticket use cases: validation, customer / category checks, the clock, sequential numbers, the customer timeline,
/// storage through <see cref="ITicketRepository"/>.
/// </summary>
public sealed class TicketService(
    ITicketRepository tickets,
    ITicketCategoryRepository categories,
    IInteractionRecorder timeline,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CreateTicketRequest> createValidator) : ITicketService
{
    /// <summary>
    /// One ticket at a time takes "highest number + 1" and saves, so tickets created through this API instance never
    /// collide. The unique index on the number still guards several instances (→ ConflictException, 409).
    /// </summary>
    private static readonly SemaphoreSlim NumberLock = new(1, 1);

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customerId = request.CustomerId!.Value;
        if (!await tickets.CustomerExistsAsync(customerId, cancellationToken))
        {
            throw FieldError("customerId", TicketText.CustomerNotFound);
        }

        if (request.CategoryId is { } categoryId
            && await categories.FindAsync(categoryId, cancellationToken) is not { IsActive: true })
        {
            throw FieldError("categoryId", TicketText.CategoryUnavailable);
        }

        var priority = TicketValues.TryParsePriority(request.Priority, out var parsed) ? parsed : TicketPriority.Mid;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var ticket = Ticket.Create(customerId, request.Subject!, request.Description, request.CategoryId, priority,
            TicketChannel.Manual, currentUser.UserId, now);

        await NumberLock.WaitAsync(cancellationToken);
        try
        {
            ticket.AssignNumber(await tickets.NextNumberAsync(cancellationToken));
            tickets.Add(ticket);
            // CRM-10 AC 2: the ticket shows in the customer's timeline; the entry is saved together with the ticket.
            timeline.Record(customerId, InteractionType.Ticket, InteractionEvents.TicketCreated,
                $"{ticket.DisplayNumber} {ticket.Subject}", ticket.Id, now);
            await tickets.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            NumberLock.Release();
        }

        return await GetAsync(ticket.Id, cancellationToken);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await tickets.GetViewAsync(id, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound));

    /// <summary>The API shape of a ticket view (also used by the list).</summary>
    public static TicketResponse ToResponse(TicketView view)
    {
        var ticket = view.Ticket;
        return new TicketResponse(
            ticket.Id,
            ticket.DisplayNumber,
            ticket.Subject,
            ticket.Description,
            TicketValues.StatusName(ticket.Status),
            TicketValues.PriorityName(ticket.Priority),
            TicketValues.ChannelName(ticket.Channel),
            ticket.CustomerId,
            view.CustomerName,
            ticket.CategoryId,
            view.CategoryName,
            ticket.AssigneeId,
            view.AssigneeName,
            ticket.CreatedAt,
            ticket.UpdatedAt);
    }

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
