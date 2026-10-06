using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Security;
using Crm.Application.Portal;
using Crm.Application.Tickets;
using Crm.Domain.KnowledgeBase;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.KnowledgeBase;

/// <summary>
/// Body of POST /api/tickets/{id}/articles: the published article to insert; <c>Language</c> ("en" | "ar", default the
/// request language) picks the version (the other one when it is missing).
/// </summary>
public sealed record LinkArticleRequest(Guid? ArticleId, string? Language);

/// <summary>
/// What the reply box inserts: <c>InsertText</c> = title, summary (≤ 200 characters) and the portal link of the article,
/// each on its own line, starting with a blank line.
/// </summary>
public sealed record LinkedArticleResponse(
    Guid Id, Guid ArticleId, string Title, string Summary, string Url, string InsertText, DateTime LinkedAt);

/// <summary>An article linked to a ticket (newest first); <c>LinkedByName</c> is null when the user is unknown.</summary>
public sealed record TicketArticleResponse(
    Guid Id, Guid ArticleId, string Title, DateTime LinkedAt, Guid? LinkedById, string? LinkedByName);

/// <summary>Link storage (EF Core in Crm.Infrastructure). Saves the link together with the article's counter.</summary>
public interface ITicketArticleRepository
{
    Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken);

    void Add(TicketArticleLink link);

    /// <summary>The articles linked to the ticket, newest first, with the title in the request language.</summary>
    Task<IReadOnlyList<TicketArticleResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Inserting knowledge base articles into ticket replies (CRM-39; linking needs <c>tickets.manage</c>, the list
/// <c>tickets.view</c> — enforced by the API). Failures: <c>ValidationException</c> 400 (no article id, unpublished
/// article, unknown language), <c>NotFoundException</c> 404 (unknown ticket or article).
/// </summary>
public interface ITicketArticleService
{
    Task<LinkedArticleResponse> LinkAsync(Guid ticketId, LinkArticleRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketArticleResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class TicketArticleService(
    ITicketArticleRepository links,
    IKbArticleRepository articles,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    PortalOptions portal) : ITicketArticleService
{
    public const int SummaryLength = 200;

    public async Task<LinkedArticleResponse> LinkAsync(Guid ticketId, LinkArticleRequest request, CancellationToken cancellationToken)
    {
        if (request.ArticleId is not { } articleId || articleId == Guid.Empty)
        {
            throw FieldError("articleId", KbText.ArticleRequired);
        }

        if (request.Language is { } language
            && !string.Equals(language, LocalizedText.English, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(language, LocalizedText.Arabic, StringComparison.OrdinalIgnoreCase))
        {
            throw FieldError("language", KbText.LanguageInvalid);
        }

        await EnsureTicketAsync(ticketId, cancellationToken);
        var article = await articles.FindAsync(articleId, cancellationToken) ?? throw new NotFoundException(KbText.ArticleNotFound);
        if (!article.IsPublished)
        {
            throw FieldError("articleId", KbText.ArticleNotPublished);
        }

        var arabic = request.Language is null ? KbLanguage.IsArabic : string.Equals(request.Language, LocalizedText.Arabic, StringComparison.OrdinalIgnoreCase);
        var title = arabic ? Prefer(article.TitleAr, article.TitleEn) : Prefer(article.TitleEn, article.TitleAr);
        var body = arabic ? Prefer(article.BodyAr, article.BodyEn) : Prefer(article.BodyEn, article.BodyAr);
        var summary = body.Length <= SummaryLength ? body : body[..SummaryLength];
        var url = portal.Link($"portal/kb/articles/{article.Id}");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var link = TicketArticleLink.Create(ticketId, article.Id, currentUser.UserId, now);
        article.RecordLinked();
        links.Add(link);
        await links.SaveChangesAsync(cancellationToken);

        var insertText = string.Join("\n", string.Empty, string.Empty, title, summary, url, string.Empty);
        return new LinkedArticleResponse(link.Id, article.Id, title, summary, url, insertText, now);
    }

    public async Task<IReadOnlyList<TicketArticleResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureTicketAsync(ticketId, cancellationToken);
        return await links.ListAsync(ticketId, cancellationToken);
    }

    private async Task EnsureTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!await links.TicketExistsAsync(ticketId, cancellationToken))
        {
            throw new NotFoundException(TicketText.NotFound);
        }
    }

    private static string Prefer(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
