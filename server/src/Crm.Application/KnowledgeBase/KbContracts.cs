using Crm.Domain.KnowledgeBase;

namespace Crm.Application.KnowledgeBase;

/// <summary>Body of POST / PUT /api/kb/categories: a name in English and/or Arabic (at least one, max 100 characters each).</summary>
public sealed record KbCategoryRequest(string? NameEn, string? NameAr);

/// <summary>A category. <c>Name</c> is the version for the request language; <c>ArticleCount</c> counts its (non-deleted) articles.</summary>
public sealed record KbCategoryResponse(Guid Id, string? NameEn, string? NameAr, string Name, int ArticleCount);

/// <summary>
/// Body of POST / PUT /api/kb/articles. A language version is a title + a body (both or neither); at least one
/// version is required, the category must exist.
/// </summary>
public sealed record KbArticleRequest(Guid? CategoryId, string? TitleEn, string? BodyEn, string? TitleAr, string? BodyAr);

/// <summary>
/// GET /api/kb/articles query string: <c>categoryId</c>, <c>status</c> ("draft" | "published"; users without
/// <c>kb.manage</c> only ever get published articles), <c>search</c> (contains, in titles), <c>page</c>, <c>pageSize</c>.
/// </summary>
public sealed record ListKbArticlesQuery(Guid? CategoryId, string? Status, string? Search, int? Page, int? PageSize);

/// <summary>
/// An article for staff: both language versions (for editing) plus <c>Title</c> in the request language. <c>Status</c>
/// is "draft" or "published". Times are UTC.
/// </summary>
public sealed record KbArticleResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string? TitleEn,
    string? BodyEn,
    string? TitleAr,
    string? BodyAr,
    string Title,
    string Status,
    DateTime? PublishedAt,
    int HelpfulCount,
    int NotHelpfulCount,
    int LinkedCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>An article with its category names (read model filled by the repository).</summary>
public sealed record KbArticleView(KbArticle Article, string? CategoryNameEn, string? CategoryNameAr);

/// <summary>A category with its article count (read model filled by the repository).</summary>
public sealed record KbCategoryView(KbCategory Category, int ArticleCount);

/// <summary>The validated filters the repository applies (null = no filter).</summary>
public sealed record KbArticleFilter(Guid? CategoryId, KbArticleStatus? Status, string? Search);

public static class KbValues
{
    public static string StatusName(KbArticleStatus status) => status.ToString().ToLowerInvariant();

    public static bool TryParseStatus(string? name, out KbArticleStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(name)
               && Enum.TryParse(name.Trim(), ignoreCase: true, out status)
               && Enum.IsDefined(status);
    }
}
