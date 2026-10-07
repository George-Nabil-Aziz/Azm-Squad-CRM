using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Customers;

namespace Crm.Application.Channels;

/// <summary>Inbound pipeline shared by the email and WhatsApp channels (CRM-24, CRM-26).</summary>
public sealed class InboundMessageProcessor(
    IReceivedMessageRepository receivedMessages,
    ICustomerService customers,
    IChannelTicketService tickets,
    TimeProvider timeProvider) : IInboundMessageProcessor
{
    public async Task<InboundResult> ProcessAsync(InboundChannelMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (await receivedMessages.ExistsAsync(message.Channel, message.ExternalId.Trim(), cancellationToken))
        {
            return InboundResult.Ignored;
        }

        var (customerId, isNew) = await ResolveCustomerAsync(message, cancellationToken);
        int? ticketNumber = TicketNumberTag.TryFind(message.Subject, out var number) ? number : null;

        var received = ReceivedMessage.Create(
            message.Channel, message.ExternalId, message.From, message.FromName, message.Subject, message.Body,
            ticketNumber, customerId, message.ReceivedAt, timeProvider.GetUtcNow().UtcDateTime);
        receivedMessages.Add(received);
        if (!await receivedMessages.SaveChangesAsync(cancellationToken))
        {
            return InboundResult.Ignored; // stored by a concurrent run in the meantime
        }

        if (customerId is not { } customer)
        {
            return new InboundResult(false, received.Id, null, isNew, ticketNumber); // no customer, no ticket
        }

        var ticket = await tickets.AddInboundAsync(customer, message, ticketNumber, cancellationToken);
        received.LinkToTicket(ticket.TicketId);
        await receivedMessages.SaveChangesAsync(cancellationToken);
        return new InboundResult(false, received.Id, customerId, isNew, ticketNumber, ticket.TicketId, ticket.Created);
    }

    /// <summary>The existing customer of the sender (first by name when several match) or a new one; null when unusable.</summary>
    private async Task<(Guid? CustomerId, bool IsNew)> ResolveCustomerAsync(
        InboundChannelMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var lookup = message.Channel == ChannelKind.Email
                ? new CustomerLookupQuery(null, message.From)
                : new CustomerLookupQuery(message.From, null);
            var found = await customers.LookupAsync(lookup, cancellationToken);
            if (found.Count > 0)
            {
                return (found[0].Id, false);
            }

            var name = Name(message);
            var created = message.Channel == ChannelKind.Email
                ? await customers.CreateAsync(new CustomerRequest(name, message.From, null), cancellationToken)
                : message.Channel == ChannelKind.Sms
                    ? await customers.CreateAsync(new CustomerRequest(name, null, message.From), cancellationToken)
                    : await CreateWhatsAppCustomerAsync(name, message.From, cancellationToken);
            return (created.Id, true);
        }
        catch (ValidationException)
        {
            // Sender address / number the customer rules reject: keep the message without a customer.
            return (null, false);
        }
    }

    private async Task<CustomerResponse> CreateWhatsAppCustomerAsync(string name, string number, CancellationToken cancellationToken)
    {
        var customer = await customers.CreateAsync(new CustomerRequest(name, null, number), cancellationToken);
        await customers.AddContactAsync(customer.Id, new CustomerContactRequest("whatsapp", number, true), cancellationToken);
        return customer;
    }

    private static string Name(InboundChannelMessage message)
    {
        var name = string.IsNullOrWhiteSpace(message.FromName) ? message.From.Trim() : message.FromName.Trim();
        return name.Length <= Customer.NameMaxLength ? name : name[..Customer.NameMaxLength];
    }
}
