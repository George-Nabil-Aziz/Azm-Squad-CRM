using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// API names of the ticket enums ("high", "new", "manual", …) and their parsing (any case; numbers not accepted).
/// The client mirrors them in client/src/features/tickets/ticket-values.ts.
/// </summary>
public static class TicketValues
{
    private static readonly Dictionary<string, TicketPriority> PrioritiesByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"] = TicketPriority.High,
        ["mid"] = TicketPriority.Mid,
        ["low"] = TicketPriority.Low,
    };

    private static readonly Dictionary<string, TicketStatus> StatusesByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["new"] = TicketStatus.New,
        ["open"] = TicketStatus.Open,
        ["pending"] = TicketStatus.Pending,
        ["resolved"] = TicketStatus.Resolved,
        ["closed"] = TicketStatus.Closed,
    };

    /// <summary>Every priority's API name, highest first.</summary>
    public static IReadOnlyList<string> PriorityNames { get; } = [.. PrioritiesByName.Keys];

    /// <summary>Every status's API name, in workflow order.</summary>
    public static IReadOnlyList<string> StatusNames { get; } = [.. StatusesByName.Keys];

    public static bool TryParsePriority(string? name, out TicketPriority priority) =>
        PrioritiesByName.TryGetValue(name?.Trim() ?? string.Empty, out priority);

    public static bool TryParseStatus(string? name, out TicketStatus status) =>
        StatusesByName.TryGetValue(name?.Trim() ?? string.Empty, out status);

    public static string PriorityName(TicketPriority priority) => priority.ToString().ToLowerInvariant();

    public static string StatusName(TicketStatus status) => status.ToString().ToLowerInvariant();

    public static string ChannelName(TicketChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>"inbound", "outbound" or "internal".</summary>
    public static string DirectionName(MessageDirection direction) =>
        direction == MessageDirection.InternalNote ? "internal" : direction.ToString().ToLowerInvariant();
}
