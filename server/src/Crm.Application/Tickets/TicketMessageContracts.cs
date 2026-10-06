namespace Crm.Application.Tickets;

/// <summary>
/// Body of POST /api/tickets/{id}/messages. <c>Body</c> is required (at most 10 000 characters); <c>Internal</c>
/// (default false) makes it an internal note instead of a reply to the customer. <c>TemplateName</c> (optional, at most
/// 100 characters) sends an approved WhatsApp template instead of free text (needed outside the 24-hour window).
/// </summary>
public sealed record AddTicketMessageRequest(
    string? Body, bool? Internal, string? TemplateName = null, IReadOnlyList<Guid>? MentionedUserIds = null);

/// <summary>
/// One message of a ticket thread. <c>Direction</c> is "inbound" (from the customer), "outbound" (an agent reply) or
/// "internal" (a team-only note, <c>IsInternal</c> true). <c>AuthorName</c> is null for customer messages.
/// <c>DeliveryStatus</c> ("pending", "sent", "failed") is null when nothing is delivered. Times are UTC.
/// </summary>
public sealed record TicketMessageResponse(
    Guid Id,
    string Direction,
    bool IsInternal,
    string Body,
    string Channel,
    Guid? AuthorId,
    string? AuthorName,
    DateTime CreatedAt,
    string? DeliveryStatus);
