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

    public Task<IReadOnlyDictionary<Guid, int>> CountPublishedByCategoryAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Articles
            .Where(a => a.IsPublished && !a.IsDeleted).GroupBy(a => a.CategoryId).ToDictionary(g => g.Key, g => g.Count()));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private KbArticleView View(KbArticle article)
    {
        var category = Categories.FirstOrDefault(c => c.Id == article.CategoryId);
        return new KbArticleView(article, category?.NameEn, category?.NameAr);
    }
}

/// <summary>In-memory search storage: returns the items whose normalized text contains every term (like the repository, minus the published filter that the service repeats).</summary>
internal sealed class FakeKbSearchRepository : IKbSearchRepository
{
    public List<KbArticle> Articles { get; } = [];

    public List<KbFaq> Faqs { get; } = [];

    public int Calls { get; set; }

    public Task<KbSearchCandidates> FindCandidatesAsync(IReadOnlyList<string> terms, int max, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new KbSearchCandidates(
            [.. Articles.Where(a => terms.All(a.SearchText.Contains)).Take(max)],
            [.. Faqs.Where(f => terms.All(f.SearchText.Contains)).Take(max)]));
    }
}

/// <summary>In-memory ticket-article link storage (the repository of CRM-39).</summary>
internal sealed class FakeTicketArticleRepository : ITicketArticleRepository
{
    public HashSet<Guid> Tickets { get; } = [];

    public List<TicketArticleLink> Links { get; } = [];

    public Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.Contains(ticketId));

    public void Add(TicketArticleLink link) => Links.Add(link);

    public Task<IReadOnlyList<TicketArticleResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketArticleResponse>>(
            [.. Links.Where(l => l.TicketId == ticketId).OrderByDescending(l => l.LinkedAt)
                .Select(l => new TicketArticleResponse(l.Id, l.ArticleId, "Reset your password", l.LinkedAt, l.LinkedById, null))]);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
