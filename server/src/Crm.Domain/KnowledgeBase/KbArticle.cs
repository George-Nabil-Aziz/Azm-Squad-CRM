using Crm.Domain.Common;

namespace Crm.Domain.KnowledgeBase;

/// <summary>Draft articles are only visible to editors; Published ones to agents and customers.</summary>
public enum KbArticleStatus
{
    Draft = 1,
    Published = 2,
}

/// <summary>
/// A help article in a category, with an English and/or Arabic version (title + body each; at least one complete
/// version). New articles are Drafts. Deleting is a soft delete. The counters feed the portal ("was this helpful?",
/// CRM-43) and reports ("how often was it linked to a ticket", CRM-39). Times are UTC and come from the caller.
/// </summary>
public sealed class KbArticle : ISoftDeletable
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 20_000;

    private KbArticle()
    {
        // EF Core materializes articles through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public string? TitleEn { get; private set; }

    public string? BodyEn { get; private set; }

    public string? TitleAr { get; private set; }

    public string? BodyAr { get; private set; }

    /// <summary>Normalized text of both language versions (<see cref="KbSearchText"/>), matched by the search (CRM-38).</summary>
    public string SearchText { get; private set; } = string.Empty;

    public KbArticleStatus Status { get; private set; }

    /// <summary>When it was (last) published (UTC); null while it is a Draft that was never published.</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>"Was this helpful?" votes (CRM-43).</summary>
    public int HelpfulCount { get; private set; }

    public int NotHelpfulCount { get; private set; }

    /// <summary>How often agents inserted the article into a ticket reply (CRM-39).</summary>
    public int LinkedCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public bool IsPublished => Status == KbArticleStatus.Published;

    public static KbArticle Create(
        Guid categoryId, string? titleEn, string? bodyEn, string? titleAr, string? bodyAr, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("An article needs a category.", nameof(categoryId));
        }

        var article = new KbArticle { Id = Guid.NewGuid(), Status = KbArticleStatus.Draft, CreatedAt = utcNow };
        article.SetContent(categoryId, titleEn, bodyEn, titleAr, bodyAr, utcNow);
        return article;
    }

    public void Update(Guid categoryId, string? titleEn, string? bodyEn, string? titleAr, string? bodyAr, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        SetContent(categoryId, titleEn, bodyEn, titleAr, bodyAr, utcNow);
    }

    /// <summary>Makes the article visible to readers. Publishing a published article changes nothing.</summary>
    public void Publish(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        if (IsPublished)
        {
            return;
        }

        Status = KbArticleStatus.Published;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>Hides the article from readers again (back to Draft).</summary>
    public void Unpublish(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        if (!IsPublished)
        {
            return;
        }

        Status = KbArticleStatus.Draft;
        UpdatedAt = utcNow;
    }

    /// <summary>An agent inserted the article into a ticket reply: raises <see cref="LinkedCount"/>. Only published articles can be linked.</summary>
    public void RecordLinked()
    {
        if (!IsPublished || IsDeleted)
        {
            throw new InvalidOperationException("Only a published article can be linked to a ticket.");
        }

        LinkedCount++;
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
        Guid categoryId, string? titleEn, string? bodyEn, string? titleAr, string? bodyAr, DateTime utcNow)
    {
        var (tEn, bEn) = Version(titleEn, bodyEn);
        var (tAr, bAr) = Version(titleAr, bodyAr);
        if (tEn is null && tAr is null)
        {
            throw new ArgumentException("An article needs a title and a body in English or Arabic.");
        }

        CategoryId = categoryId;
        TitleEn = tEn;
        BodyEn = bEn;
        TitleAr = tAr;
        BodyAr = bAr;
        SearchText = KbSearchText.Build(tEn, bEn, tAr, bAr);
        UpdatedAt = utcNow;
    }

    /// <summary>A version is complete (title + body) or absent; a half version is an error.</summary>
    private static (string? Title, string? Body) Version(string? title, string? body)
    {
        var t = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        var b = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
        if ((t is null) != (b is null))
        {
            throw new ArgumentException("A language version needs both a title and a body.");
        }

        if (t?.Length > TitleMaxLength || b?.Length > BodyMaxLength)
        {
            throw new ArgumentException("The title or body is too long.");
        }

        return (t, b);
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted article cannot be changed.");
        }
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
