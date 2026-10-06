using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Delivers an agent reply on an Email or WhatsApp ticket through <see cref="IChannelSender"/> (CRM-23 / CRM-25): email
/// to the customer's primary address with "Re: subject [TKT-000001]", WhatsApp to the primary WhatsApp (else primary
/// phone) number. Other channels deliver nothing. The delivery status of the ticket message follows the outbound message
/// through <see cref="TicketDeliveryObserver"/>.
/// </summary>
public sealed class ChannelTicketReplyDispatcher(
    IChannelSender sender,
    ICustomerService customers,
    ITicketRepository tickets) : ITicketReplyDispatcher
{
    public async Task ValidateAsync(Ticket ticket, string? templateName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (await BuildReplyAsync(ticket, "-", templateName, null, cancellationToken) is { } reply)
        {
            await sender.EnsureCanSendAsync(reply, cancellationToken);
        }
    }

    public async Task DispatchAsync(TicketMessage message, string? templateName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var ticket = await tickets.FindAsync(message.TicketId, cancellationToken);
        if (ticket is null || await BuildReplyAsync(ticket, message.Body, templateName, message.Id, cancellationToken) is not { } reply)
        {
            return;
        }

        // The result reaches the ticket message through TicketDeliveryObserver (sent / failed + provider id), saved by the sender.
        await sender.SendAsync(reply, cancellationToken);
    }

    private async Task<ChannelReply?> BuildReplyAsync(
        Ticket ticket, string body, string? templateName, Guid? sourceId, CancellationToken cancellationToken)
    {
        if (ticket.Channel is not (TicketChannel.Email or TicketChannel.WhatsApp))
        {
            return null;
        }

        var customer = await customers.GetAsync(ticket.CustomerId, cancellationToken);
        if (ticket.Channel == TicketChannel.Email)
        {
            var address = Primary(customer, "email") ?? customer.Email;
            return address is null
                ? throw Missing(ChannelText.CustomerHasNoEmail)
                : new ChannelReply(ChannelKind.Email, address, TicketNumberTag.AppendTo("Re: " + ticket.Subject, ticket.Number), body, null, sourceId);
        }

        var number = Primary(customer, "whatsapp") ?? Primary(customer, "phone") ?? customer.Phone;
        return number is null
            ? throw Missing(ChannelText.CustomerHasNoWhatsApp)
            : new ChannelReply(ChannelKind.WhatsApp, number, null, body, string.IsNullOrWhiteSpace(templateName) ? null : templateName.Trim(), sourceId);
    }

    private static string? Primary(CustomerResponse customer, string type) =>
        (customer.Contacts.FirstOrDefault(c => c.Type == type && c.IsPrimary) ?? customer.Contacts.FirstOrDefault(c => c.Type == type))?.Value;

    private static ValidationException Missing(string message) =>
        new(new Dictionary<string, string[]> { ["channel"] = [message] });
}

/// <summary>
/// Keeps the delivery status of a ticket reply in step with its outbound message (<c>SourceId</c> = ticket message id):
/// sent / delivered / read → Sent, failed → Failed. Runs inside the channel sender's unit of work (shared DbContext).
/// </summary>
public sealed class TicketDeliveryObserver(ITicketMessageRepository messages) : IChannelDeliveryObserver
{
    public async Task OnDeliveryChangedAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        if (message.SourceId is not { } sourceId || await messages.FindAsync(sourceId, cancellationToken) is not { } reply)
        {
            return;
        }

        switch (message.Status)
        {
            case DeliveryStatus.Failed:
                reply.MarkFailed();
                break;
            case DeliveryStatus.Sent or DeliveryStatus.Delivered or DeliveryStatus.Read:
                reply.MarkSent(message.ProviderMessageId);
                break;
        }
    }
}
