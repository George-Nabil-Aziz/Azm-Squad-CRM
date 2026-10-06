using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;

namespace Crm.Application.KnowledgeBase;

/// <summary>
/// Body of POST / PUT /api/kb/faqs. A language version is a question + an answer (both or neither), at least one is
/// required. <c>DisplayOrder</c> (≥ 0; lowest first): empty on create = last, empty on update = unchanged.
/// <c>IsPublished</c> (default false on create, unchanged on update).
/// </summary>
public sealed record KbFaqRequest(
    string? QuestionEn, string? AnswerEn, string? QuestionAr, string? AnswerAr, int? DisplayOrder, bool? IsPublished);

/// <summary>A FAQ for staff: both language versions plus <c>Question</c> / <c>Answer</c> in the request language.</summary>
public sealed record KbFaqResponse(
    Guid Id,
    string? QuestionEn,
    string? AnswerEn,
    string? QuestionAr,
    string? AnswerAr,
    string Question,
    string Answer,
    int DisplayOrder,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>A published FAQ as the portal shows it, in the request language (the other language when a version is missing).</summary>
public sealed record PortalFaqResponse(Guid Id, string Question, string Answer);

/// <summary>FAQ storage (EF Core in Crm.Infrastructure). Deleted FAQs are never returned.</summary>
public interface IKbFaqRepository
{
    /// <summary>FAQs ordered by display order (then creation time); only the published ones when asked.</summary>
    Task<IReadOnlyList<KbFaq>> ListAsync(bool publishedOnly, CancellationToken cancellationToken);

    /// <summary>The tracked FAQ, or null.</summary>
    Task<KbFaq?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Highest display order + 1 (1 for the first FAQ).</summary>
    Task<int> NextDisplayOrderAsync(CancellationToken cancellationToken);

    void Add(KbFaq faq);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// FAQs (reading needs <c>kb.view</c> — published only without <c>kb.manage</c>; writing <c>kb.manage</c>; the portal
/// read is anonymous). Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404.
/// </summary>
public interface IKbFaqService
{
    Task<IReadOnlyList<KbFaqResponse>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PortalFaqResponse>> ListPublishedAsync(CancellationToken cancellationToken);

    Task<KbFaqResponse> CreateAsync(KbFaqRequest request, CancellationToken cancellationToken);

    Task<KbFaqResponse> UpdateAsync(Guid id, KbFaqRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class KbFaqRequestValidator : AbstractValidator<KbFaqRequest>
{
    public KbFaqRequestValidator()
    {
        RuleFor(x => x.QuestionEn).MaximumLength(KbFaq.QuestionMaxLength).WithName(_ => KbText.QuestionField);
        RuleFor(x => x.QuestionAr).MaximumLength(KbFaq.QuestionMaxLength).WithName(_ => KbText.QuestionField);
        RuleFor(x => x.AnswerEn).MaximumLength(KbFaq.AnswerMaxLength).WithName(_ => KbText.AnswerField);
        RuleFor(x => x.AnswerAr).MaximumLength(KbFaq.AnswerMaxLength).WithName(_ => KbText.AnswerField);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithName(_ => KbText.DisplayOrderField);
        RuleFor(x => x).Custom((request, context) =>
        {
            if (string.IsNullOrWhiteSpace(request.QuestionEn) && string.IsNullOrWhiteSpace(request.QuestionAr))
            {
                context.AddFailure("question", KbText.QuestionRequired);
            }

            CheckPair(context, request.QuestionEn, request.AnswerEn, "questionEn", "answerEn");
            CheckPair(context, request.QuestionAr, request.AnswerAr, "questionAr", "answerAr");
        });
    }

    private static void CheckPair(
        ValidationContext<KbFaqRequest> context, string? question, string? answer, string questionField, string answerField)
    {
        var hasQuestion = !string.IsNullOrWhiteSpace(question);
        var hasAnswer = !string.IsNullOrWhiteSpace(answer);
        if (hasQuestion && !hasAnswer)
        {
            context.AddFailure(answerField, KbText.AnswerRequired);
        }
        else if (hasAnswer && !hasQuestion)
        {
            context.AddFailure(questionField, KbText.QuestionRequiredForAnswer);
        }
    }
}

public sealed class KbFaqService(
    IKbFaqRepository faqs,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<KbFaqRequest> validator) : IKbFaqService
{
    public async Task<IReadOnlyList<KbFaqResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var publishedOnly = !currentUser.HasPermission(Permissions.KbManage);
        return [.. (await faqs.ListAsync(publishedOnly, cancellationToken)).Select(ToResponse)];
    }

    public async Task<IReadOnlyList<PortalFaqResponse>> ListPublishedAsync(CancellationToken cancellationToken) =>
        [.. (await faqs.ListAsync(publishedOnly: true, cancellationToken)).Select(faq => new PortalFaqResponse(
            faq.Id, KbLanguage.Pick(faq.QuestionEn, faq.QuestionAr), KbLanguage.Pick(faq.AnswerEn, faq.AnswerAr)))];

    public async Task<KbFaqResponse> CreateAsync(KbFaqRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var order = request.DisplayOrder ?? await faqs.NextDisplayOrderAsync(cancellationToken);
        var faq = KbFaq.Create(
            request.QuestionEn, request.AnswerEn, request.QuestionAr, request.AnswerAr, order, request.IsPublished == true, UtcNow());
        faqs.Add(faq);
        await faqs.SaveChangesAsync(cancellationToken);
        return ToResponse(faq);
    }

    public async Task<KbFaqResponse> UpdateAsync(Guid id, KbFaqRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var faq = await FindAsync(id, cancellationToken);
        faq.Update(
            request.QuestionEn, request.AnswerEn, request.QuestionAr, request.AnswerAr,
            request.DisplayOrder ?? faq.DisplayOrder, request.IsPublished ?? faq.IsPublished, UtcNow());
        await faqs.SaveChangesAsync(cancellationToken);
        return ToResponse(faq);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var faq = await FindAsync(id, cancellationToken);
        faq.Delete(UtcNow());
        await faqs.SaveChangesAsync(cancellationToken);
    }

    private async Task<KbFaq> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await faqs.FindAsync(id, cancellationToken) ?? throw new NotFoundException(KbText.FaqNotFound);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static KbFaqResponse ToResponse(KbFaq faq) => new(
        faq.Id,
        faq.QuestionEn,
        faq.AnswerEn,
        faq.QuestionAr,
        faq.AnswerAr,
        KbLanguage.Pick(faq.QuestionEn, faq.QuestionAr),
        KbLanguage.Pick(faq.AnswerEn, faq.AnswerAr),
        faq.DisplayOrder,
        faq.IsPublished,
        faq.CreatedAt,
        faq.UpdatedAt);
}
