namespace Crm.Domain.Portal;

public enum SurveyState
{
    Open,
    Answered,
    Expired,
}

public enum SurveyRateResult
{
    Ok,
    InvalidRating,
    AlreadyRated,
    Expired,
}

/// <summary>
/// The satisfaction survey of a resolved ticket (CRM-44): one per ticket. The <see cref="Token"/> in the emailed link is its
/// credential. A rating 1..5 with an optional comment is saved once; the survey expires at <see cref="ExpiresAt"/>
/// (seven days after it was sent). Times are UTC and come from the caller.
/// </summary>
public sealed class TicketSurvey
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int CommentMaxLength = 2000;
    public const int TokenMaxLength = 64;

    private TicketSurvey()
    {
        // EF Core materializes surveys through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    /// <summary>Random, unguessable; part of the survey link.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>When the survey was (last) sent.</summary>
    public DateTime IssuedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public int? Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime? RatedAt { get; private set; }

    public static TicketSurvey Issue(Guid ticketId, string token, DateTime utcNow, TimeSpan validity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        EnsureUtc(utcNow);
        return new TicketSurvey { Id = Guid.NewGuid(), TicketId = ticketId, Token = token, IssuedAt = utcNow, ExpiresAt = utcNow + validity };
    }

    public SurveyState StateAt(DateTime utcNow) =>
        Rating is not null ? SurveyState.Answered : utcNow >= ExpiresAt ? SurveyState.Expired : SurveyState.Open;

    /// <summary>Saves the rating when it is 1..5, the survey is unanswered and not expired; otherwise says why not and changes nothing.</summary>
    public SurveyRateResult Rate(int rating, string? comment, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Rating is not null)
        {
            return SurveyRateResult.AlreadyRated;
        }

        if (utcNow >= ExpiresAt)
        {
            return SurveyRateResult.Expired;
        }

        if (rating is < MinRating or > MaxRating)
        {
            return SurveyRateResult.InvalidRating;
        }

        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        RatedAt = utcNow;
        return SurveyRateResult.Ok;
    }

    /// <summary>The ticket was resolved again before the customer answered: new expiry, same link. False for an answered survey.</summary>
    public bool Renew(DateTime utcNow, TimeSpan validity)
    {
        EnsureUtc(utcNow);
        if (Rating is not null)
        {
            return false;
        }

        IssuedAt = utcNow;
        ExpiresAt = utcNow + validity;
        return true;
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
