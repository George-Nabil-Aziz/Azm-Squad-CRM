namespace Crm.Domain.Ai;

/// <summary>
/// The AI summary of a ticket (CRM-50): one row per ticket; regenerating replaces the text and moves
/// <see cref="GeneratedAt"/>. Times are UTC and come from the caller.
/// </summary>
public sealed class TicketAiSummary
{
    public const int TextMaxLength = 4_000;
    public const int LanguageMaxLength = 8;

    private TicketAiSummary()
    {
        // EF Core materializes summaries through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>"ar" or "en": the language the summary was asked for (the ticket language).</summary>
    public string Language { get; private set; } = "en";

    public DateTime GeneratedAt { get; private set; }

    /// <summary>The staff user who asked for it; null when unknown.</summary>
    public Guid? GeneratedById { get; private set; }

    public static TicketAiSummary Create(Guid ticketId, string text, string language, Guid? generatedById, DateTime utcNow)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A summary needs a ticket.", nameof(ticketId));
        }

        var summary = new TicketAiSummary { Id = Guid.NewGuid(), TicketId = ticketId };
        summary.Replace(text, language, generatedById, utcNow);
        return summary;
    }

    /// <summary>Replaces the saved summary (regenerating). Text is trimmed and cut to <see cref="TextMaxLength"/>.</summary>
    public void Replace(string text, string language, Guid? generatedById, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        var trimmed = text.Trim();
        Text = trimmed.Length > TextMaxLength ? trimmed[..TextMaxLength] : trimmed;
        Language = language.Trim().ToLowerInvariant();
        GeneratedById = generatedById;
        GeneratedAt = utcNow;
    }
}
