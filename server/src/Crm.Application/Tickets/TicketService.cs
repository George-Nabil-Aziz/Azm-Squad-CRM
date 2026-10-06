using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Application.Sla;
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
    IValidator<CreateTicketRequest> createValidator,
    IValidator<ListTicketsQuery> listValidator,
    ISlaPolicyRepository slaPolicies) : ITicketService
{
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
        // CRM-20: due times come from the policy of the priority now; later policy changes do not move them.
        if (await slaPolicies.FindAsync(priority, cancellationToken) is { } policy)
        {
            ticket.ApplySla(policy);
        }

        // CRM-10 AC 2: the ticket shows in the customer's timeline; the entry is saved together with the ticket.
        await TicketNumbering.SaveNewAsync(tickets, ticket, () => timeline.Record(
            customerId, InteractionType.Ticket, InteractionEvents.TicketCreated,
            $"{ticket.DisplayNumber} {ticket.Subject}", ticket.Id, now), cancellationToken);

        return await GetAsync(ticket.Id, cancellationToken);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await tickets.GetViewAsync(id, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound));

    public async Task<PagedResult<TicketResponse>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        var search = query.Search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            search = null;
        }

        var filter = new TicketListFilter(
            TicketValues.TryParseStatus(query.Status, out var status) ? status : null,
            TicketValues.TryParsePriority(query.Priority, out var priority) ? priority : null,
            query.CategoryId,
            query.AssigneeId,
            query.Unassigned == true,
            StartOfUtcDay(query.CreatedFrom),
            StartOfUtcDay(query.CreatedTo?.AddDays(1)),
            search,
            Ticket.TryParseNumber(search, out var number) ? number : null);

        var page = await tickets.ListAsync(
            filter,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);

        return new PagedResult<TicketResponse>([.. page.Items.Select(ToResponse)], page.Page, page.PageSize, page.TotalCount);
    }

    public Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken) =>
        tickets.ListAssigneesAsync(cancellationToken);

    public async Task<TicketResponse> ChangePriorityAsync(
        Guid id, ChangeTicketPriorityRequest request, CancellationToken cancellationToken)
    {
        if (!TicketValues.TryParsePriority(request.Priority, out var priority))
        {
            throw FieldError("priority", TicketText.PriorityInvalid);
        }

        var ticket = await tickets.FindAsync(id, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        // CRM-20 AC 2: the due times are recalculated from CreatedAt with the new priority's current policy.
        ticket.ChangePriority(priority, await slaPolicies.FindAsync(priority, cancellationToken),
            timeProvider.GetUtcNow().UtcDateTime);
        await tickets.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

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
            ticket.UpdatedAt,
            ticket.ResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.FirstResponseAt,
            ticket.ResolvedAt,
            ticket.ResponseBreached,
            ticket.ResolutionBreached,
            ticket.EscalationLevel,
            ticket.ResponseWarnedAt);
    }

    private static DateTime? StartOfUtcDay(DateOnly? day) =>
        day?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
