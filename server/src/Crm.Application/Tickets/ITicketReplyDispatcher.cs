using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Delivers an agent reply to the customer through the ticket channel. <see cref="ValidateAsync"/> runs before the reply
/// is saved and throws (<c>ValidationException</c>) when it cannot be delivered at all (no address, WhatsApp 24-hour
/// window); <see cref="DispatchAsync"/> runs after the outbound reply is saved (never for internal notes) and must not
/// throw for a failed delivery: the delivery status of the message is updated instead
/// (<see cref="TicketMessage.MarkSent"/> / <see cref="TicketMessage.MarkFailed"/>).
/// </summary>
public interface ITicketReplyDispatcher
{
    Task ValidateAsync(Ticket ticket, string? templateName, CancellationToken cancellationToken);

    Task DispatchAsync(TicketMessage message, string? templateName, CancellationToken cancellationToken);
}

/// <summary>Default dispatcher for tickets without a deliverable channel: validates and delivers nothing.</summary>
public sealed class NoopTicketReplyDispatcher : ITicketReplyDispatcher
{
    public Task ValidateAsync(Ticket ticket, string? templateName, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DispatchAsync(TicketMessage message, string? templateName, CancellationToken cancellationToken) => Task.CompletedTask;
}
