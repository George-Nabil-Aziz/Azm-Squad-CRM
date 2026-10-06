namespace Crm.Domain.Sla;

/// <summary>
/// The business hours of the SLA clock (CRM-35): working days, a daily opening window and the time zone they are
/// meant in. <see cref="AddBusinessMinutes"/> counts only minutes inside the window; time outside it (nights, closed
/// days) does not run. Daylight-saving gaps are handled on wall-clock time: a result inside a skipped hour moves one
/// hour on.
/// </summary>
public sealed class BusinessCalendar
{
    private readonly HashSet<DayOfWeek> _days;

    public BusinessCalendar(IEnumerable<DayOfWeek> days, TimeOnly start, TimeOnly end, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        _days = [.. days];
        if (_days.Count == 0)
        {
            throw new ArgumentException("At least one working day is required.", nameof(days));
        }

        if (start >= end)
        {
            throw new ArgumentException("The opening time must be before the closing time.", nameof(start));
        }

        Start = start;
        End = end;
        TimeZone = timeZone;
    }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public TimeZoneInfo TimeZone { get; }

    public IReadOnlyCollection<DayOfWeek> Days => _days;

    /// <summary>The UTC time at which <paramref name="minutes"/> of business time have passed since <paramref name="startUtc"/>.</summary>
    public DateTime AddBusinessMinutes(DateTime startUtc, double minutes)
    {
        if (startUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(startUtc));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minutes, 0);

        var cursor = TimeZoneInfo.ConvertTimeFromUtc(startUtc, TimeZone);
        var remaining = minutes;
        while (true)
        {
            if (_days.Contains(cursor.DayOfWeek))
            {
                var opens = cursor.Date + Start.ToTimeSpan();
                var closes = cursor.Date + End.ToTimeSpan();
                if (cursor < opens)
                {
                    cursor = opens;
                }

                if (cursor < closes)
                {
                    var available = (closes - cursor).TotalMinutes;
                    if (remaining <= available)
                    {
                        return ToUtc(cursor.AddMinutes(remaining));
                    }

                    remaining -= available;
                }
            }

            cursor = cursor.Date.AddDays(1);
        }
    }

    private DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (TimeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1); // inside a daylight-saving gap
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone), DateTimeKind.Utc);
    }
}
