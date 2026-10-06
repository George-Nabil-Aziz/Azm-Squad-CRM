using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>API names of the ticket enums ("high", "mid", "low") and their parsing (any case; numbers not accepted).</summary>
public static class TicketValues
{
    private static readonly Dictionary<string, TicketPriority> PrioritiesByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"] = TicketPriority.High,
        ["mid"] = TicketPriority.Mid,
        ["low"] = TicketPriority.Low,
    };

    /// <summary>Every priority's API name, highest first.</summary>
    public static IReadOnlyList<string> PriorityNames { get; } = [.. PrioritiesByName.Keys];

    public static bool TryParsePriority(string? name, out TicketPriority priority) =>
        PrioritiesByName.TryGetValue(name?.Trim() ?? string.Empty, out priority);

    public static string PriorityName(TicketPriority priority) => priority.ToString().ToLowerInvariant();
}
