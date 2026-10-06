using Crm.Application.Auth;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.UnitTests.KnowledgeBase;

/// <summary>A signed-in user of a unit test with an explicit set of permissions.</summary>
internal sealed class KbUser(params string[] permissions) : ICurrentUser
{
    public static KbUser Editor => new(Permissions.KbView, Permissions.KbManage);

    public static KbUser Agent => new(Permissions.KbView);

    public Guid? UserId { get; } = Guid.NewGuid();

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => permissions.Contains(permission);
}

/// <summary>In-memory category storage with the contract of the EF Core repository (deleted categories are hidden).</summary>
internal sealed class FakeKbCategoryRepository(FakeKbArticleRepository articles) : IKbCategoryRepository
{
    public List<KbCategory> Categories { get; } = [];

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<KbCategoryView>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<KbCategoryView>>(
            [.. Categories.Where(c => !c.IsDeleted).Select(c => new KbCategoryView(c, Count(c.Id)))]);

    public Task<KbCategory?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.FirstOrDefault(c => c.Id == id && !c.IsDeleted));

    public Task<int> CountArticlesAsync(Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Count(categoryId));

    public void Add(KbCategory category) => Categories.Add(category);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    private int Count(Guid categoryId) => articles.Articles.Count(a => a.CategoryId == categoryId && !a.IsDeleted);
}

/// <summary>In-memory FAQ storage with the contract of the EF Core repository (deleted FAQs hidden, ordered by display order).</summary>
internal sealed class FakeKbFaqRepository : IKbFaqRepository
{
    public List<KbFaq> Faqs { get; } = [];

    public Task<IReadOnlyList<KbFaq>> ListAsync(bool publishedOnly, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<KbFaq>>(
            [.. Faqs.Where(f => !f.IsDeleted && (!publishedOnly || f.IsPublished)).OrderBy(f => f.DisplayOrder).ThenBy(f => f.CreatedAt)]);

    public Task<KbFaq?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Faqs.FirstOrDefault(f => f.Id == id && !f.IsDeleted));

    public Task<int> NextDisplayOrderAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Faqs.Where(f => !f.IsDeleted).Select(f => f.DisplayOrder + 1).DefaultIfEmpty(1).Max());

    public void Add(KbFaq faq) => Faqs.Add(faq);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>In-memory article storage with the contract of the EF Core repository (deleted articles are hidden).</summary>
internal sealed class FakeKbArticleRepository : IKbArticleRepository
{
    public List<KbArticle> Articles { get; } = [];

    public List<KbCategory> Categories { get; } = [];

    public Task<PagedResult<KbArticleView>> ListAsync(KbArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Articles.Where(a => !a.IsDeleted
                                       && (filter.CategoryId is null || a.CategoryId == filter.CategoryId)
                                       && (filter.Status is null || a.Status == filter.Status)
                                       && (filter.Search is null
                                           || (a.TitleEn ?? "").Contains(filter.Search, StringComparison.OrdinalIgnoreCase)
                                           || (a.TitleAr ?? "").Contains(filter.Search, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(View).ToList();
        return Task.FromResult(new PagedResult<KbArticleView>(items, page, pageSize, rows.Count));
    }

    public Task<KbArticleView?> GetViewAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Articles.FirstOrDefault(a => a.Id == id && !a.IsDeleted) is { } article ? View(article) : null);

    public Task<KbArticle?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Articles.FirstOrDefault(a => a.Id == id && !a.IsDeleted));

    public void Add(KbArticle article) => Articles.Add(article);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private KbArticleView View(KbArticle article)
    {
        var category = Categories.FirstOrDefault(c => c.Id == article.CategoryId);
        return new KbArticleView(article, category?.NameEn, category?.NameAr);
    }
}
