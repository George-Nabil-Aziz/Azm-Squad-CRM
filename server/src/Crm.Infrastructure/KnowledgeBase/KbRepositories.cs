using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.KnowledgeBase;

/// <summary>EF Core storage of knowledge base categories (deleted ones are hidden by the soft-delete filter).</summary>
public sealed class KbCategoryRepository(CrmDbContext db) : IKbCategoryRepository
{
    public async Task<IReadOnlyList<KbCategoryView>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.KbCategories.AsNoTracking()
            .OrderBy(c => c.NameEn ?? c.NameAr).ThenBy(c => c.Id)
            .Select(c => new { Category = c, Count = db.KbArticles.Count(a => a.CategoryId == c.Id) })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new KbCategoryView(r.Category, r.Count))];
    }

    public Task<KbCategory?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.KbCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<int> CountArticlesAsync(Guid categoryId, CancellationToken cancellationToken) =>
        db.KbArticles.CountAsync(a => a.CategoryId == categoryId, cancellationToken);

    public void Add(KbCategory category) => db.KbCategories.Add(category);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>EF Core storage of FAQs (deleted ones are hidden by the soft-delete filter).</summary>
public sealed class KbFaqRepository(CrmDbContext db) : IKbFaqRepository
{
    public async Task<IReadOnlyList<KbFaq>> ListAsync(bool publishedOnly, CancellationToken cancellationToken) =>
        await db.KbFaqs.AsNoTracking()
            .Where(f => !publishedOnly || f.IsPublished)
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.CreatedAt).ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

    public Task<KbFaq?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.KbFaqs.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<int> NextDisplayOrderAsync(CancellationToken cancellationToken) =>
        (await db.KbFaqs.MaxAsync(f => (int?)f.DisplayOrder, cancellationToken) ?? 0) + 1;

    public void Add(KbFaq faq) => db.KbFaqs.Add(faq);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>EF Core storage of knowledge base articles. Views read the category name also of a deleted category.</summary>
public sealed class KbArticleRepository(CrmDbContext db) : IKbArticleRepository
{
    public async Task<PagedResult<KbArticleView>> ListAsync(
        KbArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Rows();
        if (filter.CategoryId is { } categoryId)
        {
            rows = rows.Where(r => r.Article.CategoryId == categoryId);
        }

        if (filter.Status is { } status)
        {
            rows = rows.Where(r => r.Article.Status == status);
        }

        if (filter.Search is { } search)
        {
            var pattern = LikePattern.Contains(search);
            rows = rows.Where(r => EF.Functions.Like(r.Article.TitleEn!, pattern, LikePattern.EscapeCharacter)
                                   || EF.Functions.Like(r.Article.TitleAr!, pattern, LikePattern.EscapeCharacter));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(r => r.Article.CreatedAt).ThenByDescending(r => r.Article.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<KbArticleView>([.. items.Select(r => r.ToView())], page, pageSize, totalCount);
    }

    public async Task<KbArticleView?> GetViewAsync(Guid id, CancellationToken cancellationToken) =>
        (await Rows().FirstOrDefaultAsync(r => r.Article.Id == id, cancellationToken))?.ToView();

    public Task<KbArticle?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.KbArticles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(KbArticle article) => db.KbArticles.Add(article);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<ArticleRow> Rows() =>
        // IgnoreQueryFilters below switches the named filter off for the whole query, so deleted articles are excluded by hand.
        from article in db.KbArticles.AsNoTracking().Where(a => !a.IsDeleted)
        join category in db.KbCategories.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])
            on article.CategoryId equals category.Id
        select new ArticleRow { Article = article, CategoryNameEn = category.NameEn, CategoryNameAr = category.NameAr };

    private sealed class ArticleRow
    {
        public required KbArticle Article { get; init; }

        public string? CategoryNameEn { get; init; }

        public string? CategoryNameAr { get; init; }

        public KbArticleView ToView() => new(Article, CategoryNameEn, CategoryNameAr);
    }
}

/// <summary>EF Core search over the stored normalized <c>SearchText</c> of published, non-deleted articles and FAQs.</summary>
public sealed class KbSearchRepository(CrmDbContext db) : IKbSearchRepository
{
    public async Task<KbSearchCandidates> FindCandidatesAsync(
        IReadOnlyList<string> terms, int max, CancellationToken cancellationToken)
    {
        var articles = db.KbArticles.AsNoTracking().Where(a => a.Status == KbArticleStatus.Published);
        var faqs = db.KbFaqs.AsNoTracking().Where(f => f.IsPublished);
        foreach (var term in terms)
        {
            var pattern = LikePattern.Contains(term);
            articles = articles.Where(a => EF.Functions.Like(a.SearchText, pattern, LikePattern.EscapeCharacter));
            faqs = faqs.Where(f => EF.Functions.Like(f.SearchText, pattern, LikePattern.EscapeCharacter));
        }

        return new KbSearchCandidates(
            await articles.OrderByDescending(a => a.CreatedAt).Take(max).ToListAsync(cancellationToken),
            await faqs.OrderBy(f => f.DisplayOrder).Take(max).ToListAsync(cancellationToken));
    }
}
