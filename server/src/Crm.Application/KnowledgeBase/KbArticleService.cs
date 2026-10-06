using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.KnowledgeBase;

/// <summary>
/// Knowledge base articles. Reads need <c>kb.view</c>: users without <c>kb.manage</c> only ever get published articles
/// (a draft is 404 for them). Writes need <c>kb.manage</c> (enforced by the API). Failures: <c>ValidationException</c>
/// 400 (no title, half language version, unknown category), <c>NotFoundException</c> 404.
/// </summary>
public interface IKbArticleService
{
    Task<PagedResult<KbArticleResponse>> ListAsync(ListKbArticlesQuery query, CancellationToken cancellationToken);

    Task<KbArticleResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Creates the article as a Draft.</summary>
    Task<KbArticleResponse> CreateAsync(KbArticleRequest request, CancellationToken cancellationToken);

    Task<KbArticleResponse> UpdateAsync(Guid id, KbArticleRequest request, CancellationToken cancellationToken);

    Task<KbArticleResponse> PublishAsync(Guid id, CancellationToken cancellationToken);

    Task<KbArticleResponse> UnpublishAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Soft delete.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class KbArticleService(
    IKbArticleRepository articles,
    IKbCategoryRepository categories,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<KbArticleRequest> requestValidator,
    IValidator<ListKbArticlesQuery> listValidator) : IKbArticleService
{
    public async Task<PagedResult<KbArticleResponse>> ListAsync(ListKbArticlesQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        KbArticleStatus? status = KbValues.TryParseStatus(query.Status, out var parsed) ? parsed : null;
        if (!currentUser.HasPermission(Permissions.KbManage))
        {
            status = KbArticleStatus.Published; // drafts are for editors only
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var page = await articles.ListAsync(
            new KbArticleFilter(query.CategoryId, status, search),
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);
        return new PagedResult<KbArticleResponse>([.. page.Items.Select(ToResponse)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<KbArticleResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var view = await articles.GetViewAsync(id, cancellationToken);
        if (view is null || (!view.Article.IsPublished && !currentUser.HasPermission(Permissions.KbManage)))
        {
            throw new NotFoundException(KbText.ArticleNotFound);
        }

        return ToResponse(view);
    }

    public async Task<KbArticleResponse> CreateAsync(KbArticleRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        await EnsureCategoryAsync(request.CategoryId!.Value, cancellationToken);
        var article = KbArticle.Create(
            request.CategoryId.Value, request.TitleEn, request.BodyEn, request.TitleAr, request.BodyAr, UtcNow());
        articles.Add(article);
        await articles.SaveChangesAsync(cancellationToken);
        return await LoadAsync(article.Id, cancellationToken);
    }

    public async Task<KbArticleResponse> UpdateAsync(Guid id, KbArticleRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var article = await FindAsync(id, cancellationToken);
        await EnsureCategoryAsync(request.CategoryId!.Value, cancellationToken);
        article.Update(request.CategoryId.Value, request.TitleEn, request.BodyEn, request.TitleAr, request.BodyAr, UtcNow());
        await articles.SaveChangesAsync(cancellationToken);
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<KbArticleResponse> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var article = await FindAsync(id, cancellationToken);
        article.Publish(UtcNow());
        await articles.SaveChangesAsync(cancellationToken);
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<KbArticleResponse> UnpublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var article = await FindAsync(id, cancellationToken);
        article.Unpublish(UtcNow());
        await articles.SaveChangesAsync(cancellationToken);
        return await LoadAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var article = await FindAsync(id, cancellationToken);
        article.Delete(UtcNow());
        await articles.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The API shape of an article for staff.</summary>
    public static KbArticleResponse ToResponse(KbArticleView view)
    {
        var article = view.Article;
        return new KbArticleResponse(
            article.Id,
            article.CategoryId,
            KbLanguage.Pick(view.CategoryNameEn, view.CategoryNameAr),
            article.TitleEn,
            article.BodyEn,
            article.TitleAr,
            article.BodyAr,
            KbLanguage.Pick(article.TitleEn, article.TitleAr),
            KbValues.StatusName(article.Status),
            article.PublishedAt,
            article.HelpfulCount,
            article.NotHelpfulCount,
            article.LinkedCount,
            article.CreatedAt,
            article.UpdatedAt);
    }

    private async Task<KbArticle> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await articles.FindAsync(id, cancellationToken) ?? throw new NotFoundException(KbText.ArticleNotFound);

    private async Task<KbArticleResponse> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await articles.GetViewAsync(id, cancellationToken)
                   ?? throw new InvalidOperationException("The saved article was not found."));

    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (await categories.FindAsync(categoryId, cancellationToken) is null)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["categoryId"] = [KbText.CategoryNotFound] });
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
