using Crm.Application.Channels;
using Crm.Application.Customers.Timeline;
using Crm.Application.Settings;
using Crm.Application.Sla;
using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>The ticket an inbound channel message ended up on; <c>Created</c> = the message opened a new ticket.</summary>
public sealed record ChannelTicketResult(Guid TicketId, bool Created);

/// <summary>Puts a customer's email / WhatsApp message on a ticket (CRM-24, CRM-26).</summary>
public interface IChannelTicketService
{
    /// <summary>
    /// Email with a <c>[TKT-n]</c> tag of an open ticket of the same customer, or WhatsApp with an open WhatsApp ticket
    /// of the customer → the message is added to that ticket; otherwise a new ticket (channel Email / WhatsApp) is created.
    /// </summary>
    Task<ChannelTicketResult> AddInboundAsync(
        Guid customerId, InboundChannelMessage message, int? ticketNumber, CancellationToken cancellationToken);
}

public sealed class ChannelTicketService(
    ITicketRepository tickets,
    ITicketMessageRepository messages,
    IInteractionRecorder timeline,
    ISlaPolicyRepository slaPolicies,
    TimeProvider timeProvider,
    ISystemSettingsProvider settings,
    IAutoAssignmentService? autoAssigner = null) : IChannelTicketService
{
    private const int WhatsAppSubjectLength = 80;
    private const int TimelineDetailsLength = 200;

    public async Task<ChannelTicketResult> AddInboundAsync(
        Guid customerId, InboundChannelMessage message, int? ticketNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var channel = message.Channel == ChannelKind.Email ? TicketChannel.Email : TicketChannel.WhatsApp;
        var existing = await FindTicketAsync(customerId, channel, ticketNumber, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var body = Body(message.Body);

        if (existing is not null)
        {
            existing.RecordCustomerMessage(message.ReceivedAt);
            var reply = TicketMessage.Inbound(existing.Id, body, channel, message.ExternalId, message.ReceivedAt);
            messages.Add(reply);
            timeline.Record(customerId, InteractionType.Message, InteractionEvents.MessageReceived, Shorten(body), reply.Id, now);
            await tickets.SaveChangesAsync(cancellationToken);
            return new ChannelTicketResult(existing.Id, false);
        }

        var ticket = Ticket.Create(
            customerId, Subject(message), Cut(body, Ticket.DescriptionMaxLength), null, TicketPriority.Mid, channel, null, now);
        var runtime = await settings.GetAsync(cancellationToken); // CRM-35: business hours + ticket prefix
        if (await slaPolicies.FindAsync(ticket.Priority, cancellationToken) is { } policy)
        {
            ticket.ApplySla(policy, runtime.Calendar); // CRM-20: due times from the policy of the priority now
        }

        var autoAssignee = autoAssigner is null ? null : await autoAssigner.TryAssignAsync(ticket, now, cancellationToken); // CRM-27

        var first = TicketMessage.Inbound(ticket.Id, body, channel, message.ExternalId, message.ReceivedAt);
        await TicketNumbering.SaveNewAsync(tickets, ticket, runtime.TicketPrefix, () =>
        {
            messages.Add(first);
            timeline.Record(customerId, InteractionType.Ticket, InteractionEvents.TicketCreated,
                $"{ticket.DisplayNumber} {ticket.Subject}", ticket.Id, now);
            timeline.Record(customerId, InteractionType.Message, InteractionEvents.MessageReceived, Shorten(body), first.Id, now);
        }, cancellationToken);
        if (autoAssigner is not null && autoAssignee is { } agent)
        {
            await autoAssigner.NotifyAssignedAsync(ticket.Id, agent, now, cancellationToken); // CRM-28
        }

        return new ChannelTicketResult(ticket.Id, true);
    }

    private async Task<Ticket?> FindTicketAsync(
        Guid customerId, TicketChannel channel, int? ticketNumber, CancellationToken cancellationToken)
    {
        if (channel == TicketChannel.WhatsApp)
        {
            return await tickets.FindLatestOpenAsync(customerId, channel, cancellationToken);
        }

        // Email: the tag must name a ticket of the same customer that still takes messages; anything else opens a new ticket.
        return ticketNumber is { } number
               && await tickets.FindByNumberAsync(number, cancellationToken) is { } ticket
               && ticket.CustomerId == customerId && ticket.AcceptsMessages
            ? ticket
            : null;
    }

    private static string Subject(InboundChannelMessage message)
    {
        if (message.Channel == ChannelKind.WhatsApp)
        {
            var text = message.Body.Trim();
            return text.Length == 0 ? ChannelText.WhatsAppSubject : Cut(text.ReplaceLineEndings(" "), WhatsAppSubjectLength);
        }

        var subject = TicketNumberTagPattern.Remove(message.Subject).Trim();
        return subject.Length == 0 ? ChannelText.NoSubject : Cut(subject, Ticket.SubjectMaxLength);
    }

    private static string Body(string text) =>
        string.IsNullOrWhiteSpace(text) ? ChannelText.NoText : Cut(text.Trim(), TicketMessage.BodyMaxLength);

    private static string Shorten(string text) => Cut(text, TimelineDetailsLength);

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>Removes the "[TKT-n]" tag from a subject.</summary>
internal static class TicketNumberTagPattern
{
    public static string Remove(string? subject) =>
        subject is null ? string.Empty : System.Text.RegularExpressions.Regex.Replace(
            subject, @"\s*\[TKT-\d{1,9}\]", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
