using Crm.Application.Common.Exceptions;
using Crm.Application.Reports;

namespace Crm.UnitTests.Reports;

internal sealed class FakeCsatReadModel : ICsatReadModel
{
    public CsatSnapshot Snapshot { get; set; } = new([], 0);

    public CsatFilter? LastFilter { get; private set; }

    public Task<CsatSnapshot> GetAsync(CsatFilter filter, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(Snapshot);
    }
}

public class CsatReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Sara = Guid.NewGuid();
    private static readonly Guid Omar = Guid.NewGuid();
    private static readonly Guid Billing = Guid.NewGuid();
    private readonly FakeCsatReadModel _csat = new();
    private readonly CsatReportService _service;

    public CsatReportServiceTests()
    {
        _service = new CsatReportService(_csat, new ReportClock(Now));
    }

    private static CsatRating Rating(int stars, int day, Guid? agent = null, string? agentName = null, Guid? category = null,
        string? categoryName = null, string? comment = null, int hour = 9) =>
        new(Guid.NewGuid(), "TKT-0000" + day + hour, stars, comment, new DateTime(2026, 10, day, hour, 0, 0, DateTimeKind.Utc),
            agent, agentName, category, categoryName);

    private Task<CsatReportResponse> GetAsync(string from = "2026-10-01", string to = "2026-10-03") =>
        _service.GetAsync(new CsatQuery(DateOnly.Parse(from), DateOnly.Parse(to)), CancellationToken.None);

    [Fact]
    public async Task WithoutRatings_EverythingIsZeroOrNull_AndEveryDayAndStarIsListed()
    {
        var report = await GetAsync();

        Assert.Equal(0, report.TotalRatings);
        Assert.Null(report.AverageRating);
        Assert.Null(report.ResponseRatePercent);
        Assert.Equal([1, 2, 3, 4, 5], report.Distribution.Select(d => d.Rating));
        Assert.All(report.Distribution, d => Assert.Equal(0, d.Count));
        Assert.Equal(3, report.ByDay.Count);
        Assert.All(report.ByDay, d => Assert.Null(d.AverageRating));
        Assert.Empty(report.ByAgent);
        Assert.Empty(report.LowRatings);
    }

    [Fact]
    public async Task AverageAndDistribution_AreComputed()
    {
        _csat.Snapshot = new CsatSnapshot([Rating(5, 1), Rating(4, 1), Rating(4, 2), Rating(1, 3)], 0);

        var report = await GetAsync();

        Assert.Equal(4, report.TotalRatings);
        Assert.Equal(3.5, report.AverageRating);
        Assert.Equal([1, 0, 0, 2, 1], report.Distribution.Select(d => d.Count));
    }

    [Fact]
    public async Task ByDay_ListsEveryDay_WithTheDaysAverage()
    {
        _csat.Snapshot = new CsatSnapshot([Rating(5, 1), Rating(4, 1), Rating(2, 3)], 0);

        var report = await GetAsync();

        Assert.Equal(["2026-10-01", "2026-10-02", "2026-10-03"], report.ByDay.Select(d => d.Date.ToString("yyyy-MM-dd")));
        Assert.Equal([4.5, null, 2.0], report.ByDay.Select(d => d.AverageRating));
        Assert.Equal([2, 0, 1], report.ByDay.Select(d => d.Count));
    }

    [Fact]
    public async Task ByAgentAndByCategory_GroupTheRatings_WithANullBucket()
    {
        _csat.Snapshot = new CsatSnapshot(
        [
            Rating(5, 1, Sara, "Sara", Billing, "Billing"),
            Rating(3, 1, Sara, "Sara", null),
            Rating(2, 2, Omar, "Omar", Billing, "Billing"),
            Rating(4, 2),
        ], 0);

        var report = await GetAsync();

        var sara = report.ByAgent.Single(g => g.Id == Sara);
        Assert.Equal(("Sara", 4.0, 2), (sara.Name, sara.AverageRating, sara.Count));
        Assert.Equal(2.0, report.ByAgent.Single(g => g.Id == Omar).AverageRating);
        Assert.Equal(4.0, report.ByAgent.Single(g => g.Id is null).AverageRating);
        var billing = report.ByCategory.Single(g => g.Id == Billing);
        Assert.Equal((3.5, 2), (billing.AverageRating, billing.Count));
        Assert.Equal(2, report.ByCategory.Single(g => g.Id is null).Count);
    }

    [Fact]
    public async Task LowRatings_AreOnesAndTwos_NewestFirst_WithTheirComments()
    {
        _csat.Snapshot = new CsatSnapshot(
        [
            Rating(1, 1, comment: "Slow"),
            Rating(2, 3, comment: null, hour: 8),
            Rating(3, 2, comment: "Fine"),
            Rating(2, 3, comment: "Not solved", hour: 15),
        ], 0);

        var report = await GetAsync();

        Assert.Equal([2, 2, 1], report.LowRatings.Select(r => r.Rating));
        Assert.Equal("Not solved", report.LowRatings[0].Comment);
        Assert.Null(report.LowRatings[1].Comment);
        Assert.Equal("Slow", report.LowRatings[2].Comment);
    }

    [Theory]
    [InlineData(4, 10, 40.0)]
    [InlineData(1, 3, 33.3)]
    public async Task ResponseRate_IsRatingsOverSurveysSent(int ratings, int sent, double expected)
    {
        _csat.Snapshot = new CsatSnapshot([.. Enumerable.Range(0, ratings).Select(_ => Rating(5, 1))], sent);

        var report = await GetAsync();

        Assert.Equal(sent, report.SurveysSent);
        Assert.Equal(expected, report.ResponseRatePercent);
    }

    [Fact]
    public async Task TheReadModel_GetsTheUtcRange()
    {
        await GetAsync();

        Assert.Equal(
            new CsatFilter(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)),
            _csat.LastFilter);
    }

    [Fact]
    public async Task BadRange_IsAValidationError()
    {
        await Assert.ThrowsAsync<ValidationException>(() => GetAsync("2026-10-05", "2026-10-01"));
    }
}
