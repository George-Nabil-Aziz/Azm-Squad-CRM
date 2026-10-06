using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Gives a new ticket its sequential number and saves it. One ticket at a time takes "highest number + 1" and saves, so
/// tickets created through this API instance never collide. The unique index on the number still guards several
/// instances (→ ConflictException, 409).
/// </summary>
internal static class TicketNumbering
{
    private static readonly SemaphoreSlim NumberLock = new(1, 1);

    /// <summary>Numbers and adds the ticket, lets <paramref name="alsoSave"/> add what must be saved with it, then saves.</summary>
    public static async Task SaveNewAsync(
        ITicketRepository tickets, Ticket ticket, string prefix, Action alsoSave, CancellationToken cancellationToken)
    {
        await NumberLock.WaitAsync(cancellationToken);
        try
        {
            ticket.AssignNumber(await tickets.NextNumberAsync(cancellationToken), prefix);
            tickets.Add(ticket);
            alsoSave();
            await tickets.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            NumberLock.Release();
        }
    }
}
