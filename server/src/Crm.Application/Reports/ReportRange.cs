using Crm.Application.Common.Exceptions;

namespace Crm.Application.Reports;

/// <summary>
/// The date range of a report: whole UTC days from <see cref="From"/> to <see cref="To"/> inclusive. Queries use
/// <c>FromUtc &lt;= time &lt; ToUtcExclusive</c>.
/// </summary>
public sealed record ReportRange(DateOnly From, DateOnly To)
{
    public DateTime FromUtc => From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public DateTime ToUtcExclusive => To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>Number of days in the range (1 when from = to).</summary>
    public int Days => To.DayNumber - From.DayNumber + 1;
}

public static class ReportRangeResolver
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    /// <summary>
    /// Fills the missing dates (last 30 days, ending today UTC) and checks the range: from &lt;= to and at most
    /// <see cref="MaxDays"/> days; otherwise a <see cref="ValidationException"/> on <c>from</c> / <c>to</c>.
    /// </summary>
    public static ReportRange Resolve(DateOnly? from, DateOnly? to, TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var end = to ?? (from is { } start && start > today ? start : today);
        var begin = from ?? end.AddDays(1 - DefaultDays);

        if (begin > end)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["from"] = [ReportText.FromAfterTo] });
        }

        var range = new ReportRange(begin, end);
        if (range.Days > MaxDays)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["to"] = [ReportText.RangeTooLong] });
        }

        return range;
    }
}
