using Crm.Application.Common.Exceptions;
using Crm.Application.Reports;

namespace Crm.UnitTests.Reports;

public class ReportRangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 15, 30, 0, TimeSpan.Zero);

    private static ReportRange Resolve(DateOnly? from, DateOnly? to) => ReportRangeResolver.Resolve(from, to, new ReportClock(Now));

    [Fact]
    public void WithoutDates_ItIsTheLast30Days_UpToToday()
    {
        var range = Resolve(null, null);

        Assert.Equal(new DateOnly(2026, 10, 6), range.To);
        Assert.Equal(new DateOnly(2026, 9, 7), range.From); // 30 days including today
        Assert.Equal(new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), range.ToUtcExclusive);
        Assert.Equal(30, range.Days);
    }

    [Fact]
    public void TheEndDay_IsIncluded()
    {
        var range = Resolve(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1));

        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), range.ToUtcExclusive);
        Assert.Equal(1, range.Days);
        Assert.Equal(DateTimeKind.Utc, range.ToUtcExclusive.Kind);
    }

    [Fact]
    public void OnlyFrom_EndsToday_AndOnlyTo_Starts29DaysBefore()
    {
        Assert.Equal(new DateOnly(2026, 10, 6), Resolve(new DateOnly(2026, 10, 1), null).To);
        Assert.Equal(new DateOnly(2026, 9, 2), Resolve(null, new DateOnly(2026, 10, 1)).From);
    }

    [Fact]
    public void FromAfterTo_IsAValidationError_OnFrom()
    {
        var error = Assert.Throws<ValidationException>(() => Resolve(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 1)));

        Assert.Contains("from", error.Errors.Keys);
    }

    [Fact]
    public void MoreThan366Days_IsAValidationError_OnTo()
    {
        var error = Assert.Throws<ValidationException>(() => Resolve(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 3)));

        Assert.Contains("to", error.Errors.Keys);
        Assert.Equal(366, Resolve(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1)).Days);
    }
}
