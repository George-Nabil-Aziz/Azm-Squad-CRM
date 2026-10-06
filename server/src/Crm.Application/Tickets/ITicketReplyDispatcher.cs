using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Delivers an agent reply to the customer through the ticket channel. Called by the ticket message service after the
/// outbound reply is saved (never for internal notes). The channel stories (CRM-23..26) replace the no-op registration; an
/// implementation updates the message through <see cref="TicketMessage.MarkSent"/> / <see cref="TicketMessage.MarkFailed"/>
/// and must not throw for a failed delivery (mark it failed instead).
/// </summary>
public interface ITicketReplyDispatcher
{
    Task DispatchAsync(TicketMessage message, CancellationToken cancellationToken);
}

/// <summary>Default dispatcher: delivers nothing (manual tickets have no channel to deliver to).</summary>
public sealed class NoopTicketReplyDispatcher : ITicketReplyDispatcher
{
    public Task DispatchAsync(TicketMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
