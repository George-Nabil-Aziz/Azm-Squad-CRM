using Crm.Application.Common.Paging;
using Crm.Domain.KnowledgeBase;
using FluentValidation;

namespace Crm.Application.KnowledgeBase;

/// <summary>A name in English or Arabic (at least one), max 100 characters each.</summary>
public sealed class KbCategoryRequestValidator : AbstractValidator<KbCategoryRequest>
{
    public KbCategoryRequestValidator()
    {
        RuleFor(x => x.NameEn).MaximumLength(KbCategory.NameMaxLength).WithName(_ => KbText.NameField);
        RuleFor(x => x.NameAr).MaximumLength(KbCategory.NameMaxLength).WithName(_ => KbText.NameField);
        RuleFor(x => x).Custom((request, context) =>
        {
            if (string.IsNullOrWhiteSpace(request.NameEn) && string.IsNullOrWhiteSpace(request.NameAr))
            {
                context.AddFailure("nameEn", KbText.NameRequired);
            }
        });
    }
}

/// <summary>
/// Article rules: a category; no title in any language → error on <c>title</c> (CRM-36 AC 4); a version is a title
/// plus a body (both or neither); length limits.
/// </summary>
public sealed class KbArticleRequestValidator : AbstractValidator<KbArticleRequest>
{
    public KbArticleRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotNull().WithName(_ => KbText.CategoryField);
        RuleFor(x => x.TitleEn).MaximumLength(KbArticle.TitleMaxLength).WithName(_ => KbText.TitleField);
        RuleFor(x => x.TitleAr).MaximumLength(KbArticle.TitleMaxLength).WithName(_ => KbText.TitleField);
        RuleFor(x => x.BodyEn).MaximumLength(KbArticle.BodyMaxLength).WithName(_ => KbText.BodyField);
        RuleFor(x => x.BodyAr).MaximumLength(KbArticle.BodyMaxLength).WithName(_ => KbText.BodyField);
        RuleFor(x => x).Custom((request, context) =>
        {
            var hasTitle = !string.IsNullOrWhiteSpace(request.TitleEn) || !string.IsNullOrWhiteSpace(request.TitleAr);
            if (!hasTitle)
            {
                context.AddFailure("title", KbText.TitleRequired);
            }

            CheckPair(context, request.TitleEn, request.BodyEn, "titleEn", "bodyEn");
            CheckPair(context, request.TitleAr, request.BodyAr, "titleAr", "bodyAr");
        });
    }

    private static void CheckPair(
        ValidationContext<KbArticleRequest> context, string? title, string? body, string titleField, string bodyField)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(title);
        var hasBody = !string.IsNullOrWhiteSpace(body);
        if (hasTitle && !hasBody)
        {
            context.AddFailure(bodyField, KbText.BodyRequired);
        }
        else if (hasBody && !hasTitle)
        {
            context.AddFailure(titleField, KbText.TitleRequiredForBody);
        }
    }
}

public sealed class ListKbArticlesQueryValidator : AbstractValidator<ListKbArticlesQuery>
{
    public ListKbArticlesQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => KbValues.TryParseStatus(status, out _)).WithMessage(_ => KbText.StatusInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}
