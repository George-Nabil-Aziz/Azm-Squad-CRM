namespace Crm.Domain.Tickets;

/// <summary>
/// The status workflow: New → Open → Pending ⇄ Open → Resolved → Closed, and reopening (Resolved / Closed → Open).
/// A move not in the table is illegal (no skipping, Closed can only be reopened).
/// </summary>
public static class TicketStatusRules
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Targets = new()
    {
        [TicketStatus.New] = [TicketStatus.Open],
        [TicketStatus.Open] = [TicketStatus.Pending, TicketStatus.Resolved],
        [TicketStatus.Pending] = [TicketStatus.Open, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Open, TicketStatus.Closed],
        [TicketStatus.Closed] = [TicketStatus.Open],
    };

    /// <summary>The statuses a ticket in <paramref name="from"/> may move to, in workflow order.</summary>
    public static IReadOnlyList<TicketStatus> AllowedTargets(TicketStatus from) => Targets[from];

    public static bool CanMove(TicketStatus from, TicketStatus to) => Targets[from].Contains(to);

    /// <summary>Moving back to Open from Resolved or Closed.</summary>
    public static bool IsReopen(TicketStatus from, TicketStatus to) =>
        to == TicketStatus.Open && from is TicketStatus.Resolved or TicketStatus.Closed;
}

/// <summary>A status move the workflow does not allow (the Application layer reports it as a 400 before it gets here).</summary>
public sealed class InvalidTicketStatusTransitionException(TicketStatus from, TicketStatus to)
    : InvalidOperationException($"A {from} ticket cannot move to {to}.")
{
    public TicketStatus From { get; } = from;

    public TicketStatus To { get; } = to;
}
