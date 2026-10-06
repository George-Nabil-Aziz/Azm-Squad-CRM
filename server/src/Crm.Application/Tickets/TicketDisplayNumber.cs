using System.Globalization;

namespace Crm.Application.Tickets;

/// <summary>Reads the sequence number out of a display number ("TKT-000007", or "ACME-000007" with a custom prefix).</summary>
public static class TicketDisplayNumber
{
    public static int Sequence(string displayNumber)
    {
        var digits = new string([.. displayNumber.Reverse().TakeWhile(char.IsAsciiDigit).Reverse()]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
