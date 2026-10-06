using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Customers.Timeline;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Portal;

/// <summary>
/// A ticket as the customer sees it: no assignee, priority or SLA data. <c>CanReply</c> = New / Open / Pending;
/// <c>CanReopen</c> = Resolved and still inside <c>Portal:ReopenWindowDays</c>.
/// </summary>
public sealed record PortalTicketSummary(
    Guid Id,
    string Number,
    string Subject,
    string? Description,
    string Status,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool CanReply,
    bool CanReopen);

/// <summary>One public message of the conversation (internal notes never get here). <c>AuthorName</c> is the agent for staff replies.</summary>
public sealed record PortalMessageResponse(Guid Id, bool FromCustomer, string? AuthorName, string Body, DateTime CreatedAt);

/// <summary>Body of POST /api/portal/tickets/{id}/messages.</summary>
public sealed record PortalReplyRequest(string? Body);

/// <summary>A history line for the customer: <c>Type</c> "created" or "status" (<c>Status</c> = the new status code, null for "created").</summary>
public sealed record PortalHistoryItem(string Type, string? Status, DateTime At);

/// <summary>
/// The signed-in customer's tickets (CRM-42). Every call checks that the ticket belongs to the customer: another customer's
/// or an unknown ticket is <c>NotFoundException</c> 404. Failures: <c>ValidationException</c> 400 on <c>body</c> / <c>status</c>.
/// </summary>
public interface IPortalTicketTracker
{
    Task<PagedResult<PortalTicketSummary>> ListAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    Task<PortalTicketSummary> GetAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken);

    /// <summary>The public conversation, oldest first (no internal notes).</summary>
    Task<IReadOnlyList<PortalMessageResponse>> ListMessagesAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Creation and status changes, oldest first.</summary>
    Task<IReadOnlyList<PortalHistoryItem>> ListHistoryAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Adds the customer's reply to an open ticket (a Pending ticket becomes Open again).</summary>
    Task<PortalMessageResponse> ReplyAsync(Guid customerId, Guid ticketId, PortalReplyRequest request, CancellationToken cancellationToken);

    /// <summary>Resolved → Open, within the allowed days.</summary>
    Task<PortalTicketSummary> ReopenAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken);
}

public sealed class PortalTicketTracker(
    ITicketRepository tickets,
    ITicketMessageRepository messages,
    ITicketHistoryRepository history,
    ITicketHistoryRecorder historyRecorder,
    IInteractionRecorder timeline,
    PortalOptions options,
    TimeProvider timeProvider) : IPortalTicketTracker
{
    private const int TimelineDetailsLength = 200;

    public async Task<PagedResult<PortalTicketSummary>> ListAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, PagingDefaults.DefaultPage);
        pageSize = Math.Clamp(pageSize, 1, PagingDefaults.MaxPageSize);
        var result = await tickets.ListAsync(
            new TicketListFilter(null, null, null, null, false, null, null, null, null, customerId), page, pageSize, cancellationToken);
        var now = UtcNow();
        return new PagedResult<PortalTicketSummary>([.. result.Items.Select(view => ToSummary(view, now))], result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<PortalTicketSummary> GetAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureOwnAsync(customerId, ticketId, cancellationToken);
        return await LoadAsync(ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<PortalMessageResponse>> ListMessagesAsync(
        Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureOwnAsync(customerId, ticketId, cancellationToken);
        var list = await messages.ListAsync(ticketId, includeInternal: false, cancellationToken);
        return [.. list.Where(m => !m.IsInternal).Select(m => new PortalMessageResponse(
            m.Id, m.Direction == "inbound", m.AuthorName, m.Body, m.CreatedAt))];
    }

    public async Task<IReadOnlyList<PortalHistoryItem>> ListHistoryAsync(
        Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await EnsureOwnAsync(customerId, ticketId, cancellationToken);
        var items = await history.ListAsync(ticketId, cancellationToken);
        return
        [
            new PortalHistoryItem("created", null, ticket.CreatedAt),
            .. items.Where(item => item.Field == "status").Select(item => new PortalHistoryItem("status", item.NewValue, item.ChangedAt)),
        ];
    }

    public async Task<PortalMessageResponse> ReplyAsync(
        Guid customerId, Guid ticketId, PortalReplyRequest request, CancellationToken cancellationToken)
    {
        var ticket = await EnsureOwnAsync(customerId, ticketId, cancellationToken);
        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > TicketMessage.BodyMaxLength)
        {
            throw FieldError("body", PortalText.ReplyInvalid(TicketMessage.BodyMaxLength));
        }

        if (!ticket.AcceptsCustomerReply)
        {
            throw FieldError("status", PortalText.TicketNotOpen);
        }

        var now = UtcNow();
        var message = TicketMessage.Inbound(ticket.Id, body, TicketChannel.Portal, null, now);
        messages.Add(message);
        ticket.RecordCustomerMessage(now);
        if (ticket.Status == TicketStatus.Pending)
        {
            // The customer answered what we waited for: the ticket is worked on again.
            ticket.ChangeStatus(TicketStatus.Open, now);
            historyRecorder.Record(ticket.Id, TicketHistoryField.Status, TicketValues.StatusName(TicketStatus.Pending), TicketValues.StatusName(TicketStatus.Open), now);
        }

        timeline.Record(ticket.CustomerId, InteractionType.Message, InteractionEvents.MessageReceived, Shorten(body), message.Id, now);
        await tickets.SaveChangesAsync(cancellationToken);
        return new PortalMessageResponse(message.Id, true, null, message.Body, message.CreatedAt);
    }

    public async Task<PortalTicketSummary> ReopenAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await EnsureOwnAsync(customerId, ticketId, cancellationToken);
        var now = UtcNow();
        if (!ticket.CanBeReopenedByCustomer(now, TimeSpan.FromDays(options.ReopenWindowDays)))
        {
            throw FieldError("status", ticket.Status == TicketStatus.Resolved
                ? PortalText.ReopenWindowPassed(options.ReopenWindowDays)
                : PortalText.OnlyResolvedCanBeReopened);
        }

        ticket.ChangeStatus(TicketStatus.Open, now);
        historyRecorder.Record(ticket.Id, TicketHistoryField.Status, TicketValues.StatusName(TicketStatus.Resolved), TicketValues.StatusName(TicketStatus.Open), now);
        await tickets.SaveChangesAsync(cancellationToken);
        return await LoadAsync(ticketId, cancellationToken);
    }

    private async Task<Ticket> EnsureOwnAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken);
        return ticket is not null && ticket.CustomerId == customerId ? ticket : throw new NotFoundException(TicketText.NotFound);
    }

    private async Task<PortalTicketSummary> LoadAsync(Guid ticketId, CancellationToken cancellationToken) =>
        ToSummary(await tickets.GetViewAsync(ticketId, cancellationToken)
                  ?? throw new NotFoundException(TicketText.NotFound), UtcNow());

    private PortalTicketSummary ToSummary(TicketView view, DateTime now)
    {
        var ticket = view.Ticket;
        return new PortalTicketSummary(
            ticket.Id, ticket.DisplayNumber, ticket.Subject, ticket.Description, TicketValues.StatusName(ticket.Status), view.CategoryName,
            ticket.CreatedAt, ticket.UpdatedAt, ticket.AcceptsCustomerReply,
            ticket.CanBeReopenedByCustomer(now, TimeSpan.FromDays(options.ReopenWindowDays)));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static string Shorten(string text) => text.Length <= TimelineDetailsLength ? text : text[..TimelineDetailsLength];

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
