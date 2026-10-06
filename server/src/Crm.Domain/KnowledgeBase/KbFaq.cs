using Crm.Domain.Common;

namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// A frequently asked question with its answer, in English and/or Arabic (a version is a question + an answer, both or
/// neither; at least one version). <see cref="DisplayOrder"/> orders the list (lowest first). Only published FAQs reach
/// the portal. Deleting is a soft delete. Times are UTC and come from the caller.
/// </summary>
public sealed class KbFaq : ISoftDeletable
{
    public const int QuestionMaxLength = 300;
    public const int AnswerMaxLength = 10_000;

    private KbFaq()
    {
        // EF Core materializes FAQs through this constructor.
    }

    public Guid Id { get; private set; }

    public string? QuestionEn { get; private set; }

    public string? AnswerEn { get; private set; }

    public string? QuestionAr { get; private set; }

    public string? AnswerAr { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public static KbFaq Create(
        string? questionEn, string? answerEn, string? questionAr, string? answerAr, int displayOrder, bool isPublished, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var faq = new KbFaq { Id = Guid.NewGuid(), CreatedAt = utcNow };
        faq.SetContent(questionEn, answerEn, questionAr, answerAr, displayOrder, isPublished, utcNow);
        return faq;
    }

    public void Update(
        string? questionEn, string? answerEn, string? questionAr, string? answerAr, int displayOrder, bool isPublished, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted FAQ cannot be changed.");
        }

        SetContent(questionEn, answerEn, questionAr, answerAr, displayOrder, isPublished, utcNow);
    }

    public void Delete(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        UpdatedAt = utcNow;
    }

    private void SetContent(
        string? questionEn, string? answerEn, string? questionAr, string? answerAr, int displayOrder, bool isPublished, DateTime utcNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        var (qEn, aEn) = Version(questionEn, answerEn);
        var (qAr, aAr) = Version(questionAr, answerAr);
        if (qEn is null && qAr is null)
        {
            throw new ArgumentException("A FAQ needs a question and an answer in English or Arabic.");
        }

        QuestionEn = qEn;
        AnswerEn = aEn;
        QuestionAr = qAr;
        AnswerAr = aAr;
        DisplayOrder = displayOrder;
        IsPublished = isPublished;
        UpdatedAt = utcNow;
    }

    private static (string? Question, string? Answer) Version(string? question, string? answer)
    {
        var q = string.IsNullOrWhiteSpace(question) ? null : question.Trim();
        var a = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
        if ((q is null) != (a is null))
        {
            throw new ArgumentException("A language version needs both a question and an answer.");
        }

        if (q?.Length > QuestionMaxLength || a?.Length > AnswerMaxLength)
        {
            throw new ArgumentException("The question or answer is too long.");
        }

        return (q, a);
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
