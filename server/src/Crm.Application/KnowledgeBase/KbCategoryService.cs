using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;

namespace Crm.Application.KnowledgeBase;

/// <summary>
/// Knowledge base categories (reading needs <c>kb.view</c>, writing <c>kb.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400 (no name), <c>NotFoundException</c> 404, <c>ConflictException</c> 409
/// (deleting a category that still has articles).
/// </summary>
public interface IKbCategoryService
{
    Task<IReadOnlyList<KbCategoryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<KbCategoryResponse> CreateAsync(KbCategoryRequest request, CancellationToken cancellationToken);

    Task<KbCategoryResponse> UpdateAsync(Guid id, KbCategoryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class KbCategoryService(
    IKbCategoryRepository categories,
    TimeProvider timeProvider,
    IValidator<KbCategoryRequest> validator) : IKbCategoryService
{
    public async Task<IReadOnlyList<KbCategoryResponse>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await categories.ListAsync(cancellationToken)).Select(view => ToResponse(view.Category, view.ArticleCount))];

    public async Task<KbCategoryResponse> CreateAsync(KbCategoryRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var category = KbCategory.Create(request.NameEn, request.NameAr, timeProvider.GetUtcNow().UtcDateTime);
        categories.Add(category);
        await categories.SaveChangesAsync(cancellationToken);
        return ToResponse(category, 0);
    }

    public async Task<KbCategoryResponse> UpdateAsync(Guid id, KbCategoryRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var category = await categories.FindAsync(id, cancellationToken) ?? throw new NotFoundException(KbText.CategoryNotFoundResult);
        category.Update(request.NameEn, request.NameAr, timeProvider.GetUtcNow().UtcDateTime);
        await categories.SaveChangesAsync(cancellationToken);
        return ToResponse(category, await categories.CountArticlesAsync(id, cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await categories.FindAsync(id, cancellationToken) ?? throw new NotFoundException(KbText.CategoryNotFoundResult);
        if (await categories.CountArticlesAsync(id, cancellationToken) > 0)
        {
            throw new ConflictException(KbText.CategoryHasArticles);
        }

        category.Delete(timeProvider.GetUtcNow().UtcDateTime);
        await categories.SaveChangesAsync(cancellationToken);
    }

    private static KbCategoryResponse ToResponse(KbCategory category, int articleCount) =>
        new(category.Id, category.NameEn, category.NameAr, KbLanguage.Pick(category.NameEn, category.NameAr), articleCount);
}
