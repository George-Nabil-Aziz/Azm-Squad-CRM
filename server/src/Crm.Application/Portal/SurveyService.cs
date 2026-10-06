using System.Security.Cryptography;
using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Portal;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Portal;

/// <summary>Body of POST /api/portal/surveys/{token} and POST /api/portal/tickets/{id}/feedback: rating 1..5 and an optional comment.</summary>
public sealed record SubmitFeedbackRequest(int? Rating, string? Comment);

/// <summary>
/// A survey as the customer sees it. <c>State</c>: "open" (can be answered), "answered" (<c>Rating</c> / <c>Comment</c> are set),
/// "expired" (the link is older than the allowed days) or "none" (the ticket has no survey).
/// </summary>
public sealed record SurveyInfoResponse(
    string TicketNumber, string Subject, string State, int? Rating, string? Comment, DateTime? ExpiresAt);

/// <summary>Survey storage (EF Core in Crm.Infrastructure). Found surveys are tracked.</summary>
public interface ISurveyRepository
{
    Task<TicketSurvey?> FindByTokenAsync(string token, CancellationToken cancellationToken);

    Task<TicketSurvey?> FindByTicketAsync(Guid ticketId, CancellationToken cancellationToken);

    void Add(TicketSurvey survey);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Customer satisfaction surveys (CRM-44). Failures: <c>NotFoundException</c> 404 (unknown token, another customer's ticket),
/// <c>ValidationException</c> 400 on <c>rating</c> (outside 1..5, missing, already answered) or <c>token</c> (expired link).
/// </summary>
public interface ISurveyService
{
    /// <summary>
    /// A ticket became Resolved: issues its survey and mails the link. An answered survey is left alone; an unanswered one
    /// is renewed and mailed again.
    /// </summary>
    Task OnTicketResolvedAsync(Ticket ticket, CancellationToken cancellationToken);

    Task<SurveyInfoResponse> GetByTokenAsync(string token, CancellationToken cancellationToken);

    Task<SurveyInfoResponse> SubmitByTokenAsync(string token, SubmitFeedbackRequest request, CancellationToken cancellationToken);

    /// <summary>The state of the survey of the customer's own ticket ("none" when it has none).</summary>
    Task<SurveyInfoResponse> GetForCustomerAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken);

    Task<SurveyInfoResponse> SubmitForCustomerAsync(
        Guid customerId, Guid ticketId, SubmitFeedbackRequest request, CancellationToken cancellationToken);
}

public sealed class SurveyService(
    ISurveyRepository surveys,
    ITicketRepository tickets,
    ICustomerRepository customers,
    IChannelSender sender,
    PortalOptions options,
    TimeProvider timeProvider) : ISurveyService
{
    private TimeSpan Validity => TimeSpan.FromDays(options.SurveyValidDays);

    public async Task OnTicketResolvedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var survey = await surveys.FindByTicketAsync(ticket.Id, cancellationToken);
        if (survey is null)
        {
            survey = TicketSurvey.Issue(ticket.Id, NewToken(), now, Validity);
            surveys.Add(survey);
        }
        else if (!survey.Renew(now, Validity))
        {
            return; // already answered: one rating per ticket
        }

        await surveys.SaveChangesAsync(cancellationToken);

        var customer = await customers.FindAsync(ticket.CustomerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(customer?.Email))
        {
            return; // no address: the customer can still rate in the portal
        }

        var link = options.Link($"portal/survey/{survey.Token}");
        await sender.SendAsync(
            new ChannelReply(
                ChannelKind.Email, customer.Email, TicketNumberTag.AppendTo(SurveyText.EmailSubject, ticket.Number),
                SurveyText.EmailBody(ticket.DisplayNumber, ticket.Subject, link, options.SurveyValidDays), null, ticket.Id),
            cancellationToken);
    }

    public async Task<SurveyInfoResponse> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var survey = await surveys.FindByTokenAsync(token, cancellationToken) ?? throw new NotFoundException(SurveyText.NotFound);
        return await InfoAsync(survey, cancellationToken);
    }

    public async Task<SurveyInfoResponse> SubmitByTokenAsync(string token, SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        var survey = await surveys.FindByTokenAsync(token, cancellationToken) ?? throw new NotFoundException(SurveyText.NotFound);
        return await RateAsync(survey, request, cancellationToken);
    }

    public async Task<SurveyInfoResponse> GetForCustomerAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await OwnTicketAsync(customerId, ticketId, cancellationToken);
        var survey = await surveys.FindByTicketAsync(ticketId, cancellationToken);
        return survey is null
            ? new SurveyInfoResponse(ticket.DisplayNumber, ticket.Subject, "none", null, null, null)
            : await InfoAsync(survey, cancellationToken);
    }

    public async Task<SurveyInfoResponse> SubmitForCustomerAsync(
        Guid customerId, Guid ticketId, SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        await OwnTicketAsync(customerId, ticketId, cancellationToken);
        var survey = await surveys.FindByTicketAsync(ticketId, cancellationToken)
                     ?? throw FieldError("rating", SurveyText.NoSurvey);
        return await RateAsync(survey, request, cancellationToken);
    }

    private async Task<SurveyInfoResponse> RateAsync(TicketSurvey survey, SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (request.Comment is { Length: > TicketSurvey.CommentMaxLength })
        {
            throw FieldError("comment", SurveyText.CommentTooLong(TicketSurvey.CommentMaxLength));
        }

        var result = request.Rating is { } rating
            ? survey.Rate(rating, request.Comment, UtcNow())
            : SurveyRateResult.InvalidRating;
        switch (result)
        {
            case SurveyRateResult.InvalidRating:
                throw FieldError("rating", SurveyText.RatingInvalid);
            case SurveyRateResult.AlreadyRated:
                throw FieldError("rating", SurveyText.AlreadyRated);
            case SurveyRateResult.Expired:
                throw FieldError("token", SurveyText.Expired(options.SurveyValidDays));
        }

        await surveys.SaveChangesAsync(cancellationToken);
        return await InfoAsync(survey, cancellationToken);
    }

    private async Task<SurveyInfoResponse> InfoAsync(TicketSurvey survey, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(survey.TicketId, cancellationToken) ?? throw new NotFoundException(SurveyText.NotFound);
        var state = survey.StateAt(UtcNow()) switch
        {
            SurveyState.Answered => "answered",
            SurveyState.Expired => "expired",
            _ => "open",
        };
        return new SurveyInfoResponse(ticket.DisplayNumber, ticket.Subject, state, survey.Rating, survey.Comment, survey.ExpiresAt);
    }

    private async Task<Ticket> OwnTicketAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken);
        return ticket is not null && ticket.CustomerId == customerId ? ticket : throw new NotFoundException(TicketText.NotFound);
    }

    /// <summary>32 random characters, URL safe: the link is the credential.</summary>
    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_');

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
