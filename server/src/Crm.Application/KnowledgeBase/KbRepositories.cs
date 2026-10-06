using Crm.Application.Common.Paging;
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.KnowledgeBase;

/// <summary>Knowledge base category storage (EF Core in Crm.Infrastructure).</summary>
public interface IKbCategoryRepository
{
    /// <summary>Every category with its article count, ordered by English name then Arabic name.</summary>
    Task<IReadOnlyList<KbCategoryView>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The tracked category, or null.</summary>
    Task<KbCategory?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CountArticlesAsync(Guid categoryId, CancellationToken cancellationToken);

    void Add(KbCategory category);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Knowledge base article storage (EF Core in Crm.Infrastructure). Deleted articles are never returned.</summary>
public interface IKbArticleRepository
{
    /// <summary>One page of articles matching every filter, newest first. <c>TotalCount</c> counts every match.</summary>
    Task<PagedResult<KbArticleView>> ListAsync(KbArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The article with its category names, or null.</summary>
    Task<KbArticleView?> GetViewAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The tracked article (changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<KbArticle?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(KbArticle article);

    /// <summary>How many published, non-deleted articles each category has (categories without any are absent).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountPublishedByCategoryAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
