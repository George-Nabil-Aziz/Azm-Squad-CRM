using Crm.Domain.Common;

namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// A knowledge base category: a name in English and/or Arabic (at least one). A category that still has articles
/// cannot be deleted (the Application layer checks). Times are UTC and come from the caller.
/// </summary>
public sealed class KbCategory : ISoftDeletable
{
    public const int NameMaxLength = 100;

    private KbCategory()
    {
        // EF Core materializes categories through this constructor.
    }

    public Guid Id { get; private set; }

    public string? NameEn { get; private set; }

    public string? NameAr { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    /// <summary>Soft delete: articles that point at the category keep working. Deleting twice changes nothing.</summary>
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

    public static KbCategory Create(string? nameEn, string? nameAr, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var category = new KbCategory { Id = Guid.NewGuid(), CreatedAt = utcNow };
        category.SetNames(nameEn, nameAr, utcNow);
        return category;
    }

    public void Update(string? nameEn, string? nameAr, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        SetNames(nameEn, nameAr, utcNow);
    }

    private void SetNames(string? nameEn, string? nameAr, DateTime utcNow)
    {
        var en = Clean(nameEn);
        var ar = Clean(nameAr);
        if (en is null && ar is null)
        {
            throw new ArgumentException("A category needs a name in English or Arabic.");
        }

        NameEn = en;
        NameAr = ar;
        UpdatedAt = utcNow;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
