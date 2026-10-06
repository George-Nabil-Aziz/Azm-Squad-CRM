using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

public sealed class TicketMessageService(
    ITicketRepository tickets,
    ITicketMessageRepository messages,
    IInteractionRecorder timeline,
    ITicketReplyDispatcher dispatcher,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<AddTicketMessageRequest> validator) : ITicketMessageService
{
    private const int TimelineDetailsLength = 200;

    public async Task<IReadOnlyList<TicketMessageResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureTicketAsync(ticketId, cancellationToken);
        return await messages.ListAsync(ticketId, includeInternal: true, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketMessageResponse>> ListCustomerVisibleAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureTicketAsync(ticketId, cancellationToken);
        return await messages.ListAsync(ticketId, includeInternal: false, cancellationToken);
    }

    public async Task<TicketMessageResponse> AddAsync(Guid ticketId, AddTicketMessageRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var ticket = await EnsureTicketAsync(ticketId, cancellationToken);
        if (!ticket.AcceptsMessages)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["status"] = [TicketMessageText.TicketClosed] });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var isInternal = request.Internal == true;
        if (!isInternal)
        {
            await dispatcher.ValidateAsync(ticket, request.TemplateName, cancellationToken); // e.g. WhatsApp 24-hour window: nothing is saved
        }

        var message = TicketMessage.Staff(ticket.Id, request.Body!, isInternal, ticket.Channel, currentUser.UserId, now);
        messages.Add(message);
        if (!isInternal)
        {
            ticket.RecordAgentReply(now); // AC 3: the first public reply is the first response (SLA)
            // CRM-10: the reply shows in the customer timeline. Internal notes never do.
            timeline.Record(ticket.CustomerId, InteractionType.Message, InteractionEvents.MessageSent,
                Shorten(message.Body), message.Id, now);
        }

        await tickets.SaveChangesAsync(cancellationToken);
        if (!isInternal)
        {
            await dispatcher.DispatchAsync(message, request.TemplateName, cancellationToken);
        }

        return await messages.GetAsync(message.Id, cancellationToken)
               ?? throw new InvalidOperationException("The saved message was not found.");
    }

    /// <summary>The API shape of a message (the repository fills the author name).</summary>
    public static TicketMessageResponse ToResponse(TicketMessage message, string? authorName) => new(
        message.Id,
        TicketValues.DirectionName(message.Direction),
        message.IsInternal,
        message.Body,
        TicketValues.ChannelName(message.Channel),
        message.AuthorId,
        authorName,
        message.CreatedAt,
        message.DeliveryStatus?.ToString().ToLowerInvariant());

    private async Task<Ticket> EnsureTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);

    private static string Shorten(string text) =>
        text.Length <= TimelineDetailsLength ? text : text[..TimelineDetailsLength];
}
