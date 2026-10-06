using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Portal;

/// <summary>A category that has published articles; <c>Name</c> is in the request language.</summary>
public sealed record PortalKbCategoryResponse(Guid Id, string Name, int ArticleCount);

/// <summary>A published article in a list: title in the request language and the first 160 characters of the body.</summary>
public sealed record PortalKbArticleSummary(Guid Id, string Title, string Summary, string CategoryName);

/// <summary>A published article with its full body and the "was this helpful?" counters.</summary>
public sealed record PortalKbArticleResponse(
    Guid Id, string Title, string Body, string CategoryName, int HelpfulCount, int NotHelpfulCount, DateTime? PublishedAt);

/// <summary>Body of POST /api/portal/kb/articles/{id}/feedback.</summary>
public sealed record PortalFeedbackRequest(bool? Helpful);

/// <summary>The counters after a vote.</summary>
public sealed record PortalFeedbackResponse(int HelpfulCount, int NotHelpfulCount);

/// <summary>
/// The public knowledge base of the portal (CRM-43): anonymous, published content only (a draft is 404), in the request
/// language with the other language as fallback. FAQs and search are <c>IKbFaqService</c> / <c>IKbSearchService</c>.
/// </summary>
public interface IPortalKbService
{
    Task<IReadOnlyList<PortalKbCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<PagedResult<PortalKbArticleSummary>> ListArticlesAsync(Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken);

    Task<PortalKbArticleResponse> GetArticleAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>"Was this helpful?": raises a counter. 400 on <c>helpful</c> when missing, 404 for a draft or unknown article.</summary>
    Task<PortalFeedbackResponse> RecordFeedbackAsync(Guid id, PortalFeedbackRequest request, CancellationToken cancellationToken);
}

public sealed class PortalKbService(IKbArticleRepository articles, IKbCategoryRepository categories) : IPortalKbService
{
    private const int SummaryLength = 160;

    public async Task<IReadOnlyList<PortalKbCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var counts = await articles.CountPublishedByCategoryAsync(cancellationToken);
        return [.. (await categories.ListAsync(cancellationToken))
            .Where(view => counts.ContainsKey(view.Category.Id))
            .Select(view => new PortalKbCategoryResponse(
                view.Category.Id, KbLanguage.Pick(view.Category.NameEn, view.Category.NameAr), counts[view.Category.Id]))];
    }

    public async Task<PagedResult<PortalKbArticleSummary>> ListArticlesAsync(
        Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, PagingDefaults.DefaultPage);
        pageSize = Math.Clamp(pageSize, 1, PagingDefaults.MaxPageSize);
        var result = await articles.ListAsync(new KbArticleFilter(categoryId, KbArticleStatus.Published, null), page, pageSize, cancellationToken);
        return new PagedResult<PortalKbArticleSummary>(
            [.. result.Items.Select(view =>
            {
                var body = KbLanguage.Pick(view.Article.BodyEn, view.Article.BodyAr);
                return new PortalKbArticleSummary(
                    view.Article.Id, KbLanguage.Pick(view.Article.TitleEn, view.Article.TitleAr),
                    body.Length <= SummaryLength ? body : body[..SummaryLength], KbLanguage.Pick(view.CategoryNameEn, view.CategoryNameAr));
            })],
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<PortalKbArticleResponse> GetArticleAsync(Guid id, CancellationToken cancellationToken)
    {
        var view = await articles.GetViewAsync(id, cancellationToken);
        if (view is null || !view.Article.IsPublished)
        {
            throw new NotFoundException(KbText.ArticleNotFound);
        }

        var article = view.Article;
        return new PortalKbArticleResponse(
            article.Id, KbLanguage.Pick(article.TitleEn, article.TitleAr), KbLanguage.Pick(article.BodyEn, article.BodyAr),
            KbLanguage.Pick(view.CategoryNameEn, view.CategoryNameAr), article.HelpfulCount, article.NotHelpfulCount, article.PublishedAt);
    }

    public async Task<PortalFeedbackResponse> RecordFeedbackAsync(
        Guid id, PortalFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (request.Helpful is not { } helpful)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["helpful"] = [PortalText.HelpfulRequired] });
        }

        var article = await articles.FindAsync(id, cancellationToken);
        if (article is null || !article.IsPublished)
        {
            throw new NotFoundException(KbText.ArticleNotFound);
        }

        article.RecordFeedback(helpful);
        await articles.SaveChangesAsync(cancellationToken);
        return new PortalFeedbackResponse(article.HelpfulCount, article.NotHelpfulCount);
    }
}
