using System.Globalization;
using System.Text.RegularExpressions;

namespace Crm.Domain.Channels;

/// <summary>
/// The ticket number in an email subject, e.g. "Re: Printer broken [TKT-000001]". Outgoing replies carry it so the
/// customer's answer can be matched to the ticket (CRM-23 / CRM-24).
/// </summary>
public static partial class TicketNumberTag
{
    /// <summary>"[TKT-000001]" (six digits, more after 999 999).</summary>
    public static string Format(int number) =>
        string.Create(CultureInfo.InvariantCulture, $"[TKT-{number:D6}]");

    /// <summary>The subject with the tag at the end; a subject that already has this tag is returned unchanged.</summary>
    public static string AppendTo(string? subject, int number)
    {
        var tag = Format(number);
        var trimmed = subject?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return tag;
        }

        return TryFind(trimmed, out var existing) && existing == number ? trimmed : $"{trimmed} {tag}";
    }

    /// <summary>Reads the first "[TKT-n]" tag (case-insensitive); false when there is none or the number is not positive.</summary>
    public static bool TryFind(string? subject, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(subject))
        {
            return false;
        }

        var match = TagPattern().Match(subject);
        return match.Success
               && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number > 0;
    }

    [GeneratedRegex(@"\[TKT-(?<number>\d{1,9})\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
