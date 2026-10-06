using Crm.Domain.Portal;

namespace Crm.UnitTests.Portal;

public class TicketSurveyTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private static TicketSurvey Issue() => TicketSurvey.Issue(Guid.NewGuid(), "token-abc", Now, Validity);

    [Fact]
    public void Issue_ExpiresAfterSevenDays_AndIsOpen()
    {
        var survey = Issue();

        Assert.Equal(Now.AddDays(7), survey.ExpiresAt);
        Assert.Equal(SurveyState.Open, survey.StateAt(Now));
        Assert.Null(survey.Rating);
        Assert.Equal("token-abc", survey.Token);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Rate_AcceptsOneToFive_WithAnOptionalComment(int rating)
    {
        var survey = Issue();

        var result = survey.Rate(rating, "  Thanks  ", Now.AddHours(1));

        Assert.Equal(SurveyRateResult.Ok, result);
        Assert.Equal(rating, survey.Rating);
        Assert.Equal("Thanks", survey.Comment);
        Assert.Equal(Now.AddHours(1), survey.RatedAt);
        Assert.Equal(SurveyState.Answered, survey.StateAt(Now.AddHours(2)));
    }

    [Fact]
    public void Rate_WithoutAComment_KeepsNull()
    {
        var survey = Issue();

        survey.Rate(4, "  ", Now);

        Assert.Null(survey.Comment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rate_OutsideOneToFive_IsRejected_AndNothingIsSaved(int rating)
    {
        var survey = Issue();

        Assert.Equal(SurveyRateResult.InvalidRating, survey.Rate(rating, "x", Now));
        Assert.Null(survey.Rating);
        Assert.Equal(SurveyState.Open, survey.StateAt(Now));
    }

    [Fact]
    public void Rate_Twice_IsRejected_AndKeepsTheFirstRating()
    {
        var survey = Issue();
        survey.Rate(5, "great", Now);

        Assert.Equal(SurveyRateResult.AlreadyRated, survey.Rate(1, "changed my mind", Now.AddMinutes(1)));
        Assert.Equal(5, survey.Rating);
        Assert.Equal("great", survey.Comment);
    }

    [Fact]
    public void Rate_IsPossibleJustBeforeTheExpiry_AndNotAtIt()
    {
        var inTime = Issue();
        var late = Issue();

        Assert.Equal(SurveyRateResult.Ok, inTime.Rate(4, null, Now.AddDays(7).AddTicks(-1)));
        Assert.Equal(SurveyRateResult.Expired, late.Rate(4, null, Now.AddDays(7)));
        Assert.Equal(SurveyState.Expired, Issue().StateAt(Now.AddDays(8)));
        Assert.Null(late.Rating);
    }

    [Fact]
    public void Renew_OfAnUnansweredSurvey_ExtendsTheExpiry_AndKeepsTheToken()
    {
        var survey = Issue();

        var renewed = survey.Renew(Now.AddDays(10), Validity);

        Assert.True(renewed);
        Assert.Equal(Now.AddDays(17), survey.ExpiresAt);
        Assert.Equal(Now.AddDays(10), survey.IssuedAt);
        Assert.Equal("token-abc", survey.Token);
        Assert.Equal(SurveyState.Open, survey.StateAt(Now.AddDays(11)));
    }

    [Fact]
    public void Renew_OfAnAnsweredSurvey_ChangesNothing()
    {
        var survey = Issue();
        survey.Rate(5, null, Now);

        Assert.False(survey.Renew(Now.AddDays(10), Validity));
        Assert.Equal(Now.AddDays(7), survey.ExpiresAt);
    }

    [Fact]
    public void Issue_WithANonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => TicketSurvey.Issue(Guid.NewGuid(), "t", DateTime.Now, Validity));
}
