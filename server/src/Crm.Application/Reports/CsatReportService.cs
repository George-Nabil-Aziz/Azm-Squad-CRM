namespace Crm.Application.Reports;

/// <summary>GET /api/reports/csat: ratings given in the range (<c>yyyy-MM-dd</c>, UTC days, inclusive).</summary>
public sealed record CsatQuery(DateOnly? From, DateOnly? To, Guid? BranchId = null);

public sealed record CsatFilter(DateTime FromUtc, DateTime ToUtcExclusive, Guid? BranchId = null);

/// <summary>One customer rating (1..5) of a resolved ticket, with the agent and category the ticket had.</summary>
public sealed record CsatRating(
    Guid TicketId,
    string TicketNumber,
    int Rating,
    string? Comment,
    DateTime RatedAt,
    Guid? AgentId,
    string? AgentName,
    Guid? CategoryId,
    string? CategoryName);

/// <summary>The ratings of a range and how many surveys were sent in it (for the response rate).</summary>
public sealed record CsatSnapshot(IReadOnlyList<CsatRating> Ratings, int SurveysSent);

/// <summary>
/// Read side of the customer satisfaction ratings. The ratings are collected by CRM-44 (another branch): until it is
/// merged <see cref="EmptyCsatReadModel"/> answers "no ratings". Wire to CRM-44 on merge: register an implementation
/// over its rating table instead.
/// </summary>
public interface ICsatReadModel
{
    Task<CsatSnapshot> GetAsync(CsatFilter filter, CancellationToken cancellationToken);
}

/// <summary>No ratings yet (CRM-44 not merged).</summary>
public sealed class EmptyCsatReadModel : ICsatReadModel
{
    public Task<CsatSnapshot> GetAsync(CsatFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(new CsatSnapshot([], 0));
}

public sealed record RatingCount(int Rating, int Count);

public sealed record CsatDay(DateOnly Date, double? AverageRating, int Count);

public sealed record CsatGroup(Guid? Id, string? Name, double AverageRating, int Count);

public sealed record LowRating(
    Guid TicketId, string TicketNumber, int Rating, string? Comment, DateTime RatedAt, string? AgentName);

public sealed record CsatReportResponse(
    DateOnly From,
    DateOnly To,
    int TotalRatings,
    double? AverageRating,
    IReadOnlyList<RatingCount> Distribution,
    IReadOnlyList<CsatDay> ByDay,
    IReadOnlyList<CsatGroup> ByAgent,
    IReadOnlyList<CsatGroup> ByCategory,
    IReadOnlyList<LowRating> LowRatings,
    int SurveysSent,
    double? ResponseRatePercent);

public interface ICsatReportService
{
    Task<CsatReportResponse> GetAsync(CsatQuery query, CancellationToken cancellationToken);
}

/// <summary>Customer satisfaction: average and distribution, trend, groupings, low ratings and the survey response rate.</summary>
public sealed class CsatReportService(ICsatReadModel readModel, TimeProvider timeProvider) : ICsatReportService
{
    public const int LowRatingMax = 2;

    public async Task<CsatReportResponse> GetAsync(CsatQuery query, CancellationToken cancellationToken)
    {
        var range = ReportRangeResolver.Resolve(query.From, query.To, timeProvider);
        var snapshot = await readModel.GetAsync(new CsatFilter(range.FromUtc, range.ToUtcExclusive, query.BranchId), cancellationToken);
        var ratings = snapshot.Ratings;

        var byDay = Enumerable.Range(0, range.Days).Select(offset =>
        {
            var day = range.From.AddDays(offset);
            var ofDay = ratings.Where(r => DateOnly.FromDateTime(r.RatedAt) == day).ToList();
            return new CsatDay(day, Average(ofDay), ofDay.Count);
        }).ToList();

        return new CsatReportResponse(
            range.From,
            range.To,
            ratings.Count,
            Average(ratings),
            [.. Enumerable.Range(1, 5).Select(star => new RatingCount(star, ratings.Count(r => r.Rating == star)))],
            byDay,
            Group(ratings, r => (r.AgentId, r.AgentName)),
            Group(ratings, r => (r.CategoryId, r.CategoryName)),
            [.. ratings.Where(r => r.Rating <= LowRatingMax).OrderByDescending(r => r.RatedAt)
                .Select(r => new LowRating(r.TicketId, r.TicketNumber, r.Rating, r.Comment, r.RatedAt, r.AgentName))],
            snapshot.SurveysSent,
            snapshot.SurveysSent == 0 ? null : Math.Round(100.0 * ratings.Count / snapshot.SurveysSent, 1));
    }

    private static double? Average(IReadOnlyCollection<CsatRating> ratings) =>
        ratings.Count == 0 ? null : Math.Round(ratings.Average(r => r.Rating), 2);

    private static IReadOnlyList<CsatGroup> Group(IReadOnlyList<CsatRating> ratings, Func<CsatRating, (Guid? Id, string? Name)> key) =>
        [.. ratings.GroupBy(r => key(r).Id)
            .Select(g => new CsatGroup(g.Key, key(g.First()).Name, Math.Round(g.Average(r => r.Rating), 2), g.Count()))
            .OrderByDescending(g => g.Count).ThenBy(g => g.Id is null).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)];
}
