using System.Globalization;
using System.Text;
using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Tickets;
using Crm.Domain.Ai;
using Crm.Domain.Tickets;

namespace Crm.Application.Ai;

/// <summary>What the AI suggested for a new ticket. <c>MeetsThreshold</c>: the confidence reaches <c>Ai:ConfidenceThreshold</c>, so it may be applied.</summary>
public sealed record AiClassificationOutcome(Guid? CategoryId, TicketPriority Priority, double Confidence, bool MeetsThreshold);

/// <summary>
/// The stored suggestion of a ticket. <c>Status</c> is "none", "applied" (something was set automatically) or "suggested"
/// (shown only). All other fields are null / false for "none".
/// </summary>
public sealed record TicketAiClassificationResponse(
    string Status,
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryName,
    string? SuggestedPriority,
    double? Confidence,
    bool CategoryApplied,
    bool PriorityApplied,
    DateTime? CreatedAt,
    DateTime? CategoryOverriddenAt,
    DateTime? PriorityOverriddenAt);

/// <summary>Storage of the classifications (EF Core in Crm.Infrastructure); saved with the unit of work of the ticket.</summary>
public interface ITicketAiClassificationRepository
{
    /// <summary>The tracked classification of the ticket, or null.</summary>
    Task<TicketAiClassification?> FindAsync(Guid ticketId, CancellationToken cancellationToken);

    void Add(TicketAiClassification classification);
}

/// <summary>
/// AI categorization of new tickets (CRM-52). Nothing here may stop a ticket from being created: <see cref="ClassifyAsync"/>
/// returns null when the AI is unavailable. Reading the result needs <c>tickets.view</c> (enforced by the API).
/// </summary>
public interface IAiClassificationService
{
    /// <summary>Asks the AI; null when it is not configured, fails, times out or answers nonsense. Never throws except for cancellation.</summary>
    Task<AiClassificationOutcome?> ClassifyAsync(string subject, string? description, CancellationToken cancellationToken);

    /// <summary>Stores the result for the new ticket (added to the unit of work; the ticket save writes it).</summary>
    void Save(Guid ticketId, AiClassificationOutcome outcome, bool categoryApplied, bool priorityApplied, DateTime utcNow);

    /// <summary>A category change: recorded as an override when it differs from the AI suggestion. No-op without a classification.</summary>
    Task RecordCategoryChangeAsync(Guid ticketId, Guid? newCategoryId, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>A priority change: recorded as an override when it differs from the AI suggestion. No-op without a classification.</summary>
    Task RecordPriorityChangeAsync(Guid ticketId, TicketPriority newPriority, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>The stored suggestion. <c>NotFoundException</c> 404 for an unknown ticket.</summary>
    Task<TicketAiClassificationResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class AiClassificationService(
    IAiTextService ai,
    ITicketCategoryRepository categories,
    ITicketAiClassificationRepository repository,
    ITicketRepository tickets,
    ICurrentUser currentUser,
    AiOptions options) : IAiClassificationService
{
    /// <summary>Creating a ticket never waits longer than this for the AI.</summary>
    public static readonly TimeSpan TimeBudget = TimeSpan.FromSeconds(10);

    private const int DescriptionMaxChars = 3_000;

    public async Task<AiClassificationOutcome?> ClassifyAsync(string subject, string? description, CancellationToken cancellationToken)
    {
        if (!ai.IsConfigured)
        {
            return null;
        }

        var active = await categories.ListAsync(activeOnly: true, cancellationToken);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeBudget);
        string answer;
        try
        {
            answer = await ai.CompleteAsync(new AiRequest(SystemPrompt, UserPrompt(subject, description, active), 200), limit.Token);
        }
        catch (Exception exception) when (exception is AiFailedException or AiNotConfiguredException
                                          || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }

        return Parse(answer, active.Select(c => c.Id).ToHashSet());
    }

    public void Save(Guid ticketId, AiClassificationOutcome outcome, bool categoryApplied, bool priorityApplied, DateTime utcNow) =>
        repository.Add(TicketAiClassification.Create(
            ticketId, outcome.CategoryId, outcome.Priority, outcome.Confidence, categoryApplied, priorityApplied, utcNow));

    public async Task RecordCategoryChangeAsync(Guid ticketId, Guid? newCategoryId, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(ticketId, cancellationToken) is { } classification)
        {
            classification.RecordCategoryChange(newCategoryId, currentUser.UserId, utcNow);
        }
    }

    public async Task RecordPriorityChangeAsync(Guid ticketId, TicketPriority newPriority, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(ticketId, cancellationToken) is { } classification)
        {
            classification.RecordPriorityChange(newPriority, currentUser.UserId, utcNow);
        }
    }

    public async Task<TicketAiClassificationResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        _ = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        if (await repository.FindAsync(ticketId, cancellationToken) is not { } c)
        {
            return new TicketAiClassificationResponse("none", null, null, null, null, false, false, null, null, null);
        }

        string? categoryName = null;
        if (c.SuggestedCategoryId is { } categoryId)
        {
            categoryName = (await categories.FindAsync(categoryId, cancellationToken))?.Name;
        }

        return new TicketAiClassificationResponse(
            c.IsApplied ? "applied" : "suggested", c.SuggestedCategoryId, categoryName, TicketValues.PriorityName(c.SuggestedPriority),
            c.Confidence, c.CategoryApplied, c.PriorityApplied, c.CreatedAt, c.CategoryOverriddenAt, c.PriorityOverriddenAt);
    }

    private const string SystemPrompt =
        "You classify customer support tickets. Reply with one JSON object only and no other text: " +
        "{\"categoryId\": \"<an id from the list, or null>\", \"priority\": \"high\" | \"mid\" | \"low\", \"confidence\": <number from 0 to 1>}. " +
        "high = service down, security, money lost or an urgent deadline; mid = a normal problem; low = a question or a small request. " +
        "confidence says how sure you are of the category and priority. Placeholders such as [email] and [phone] stand for removed personal data.";

    private static string UserPrompt(string subject, string? description, IReadOnlyList<TicketCategory> active)
    {
        var text = PiiMasker.Mask(description);
        var prompt = new StringBuilder("Subject: ").AppendLine(PiiMasker.Mask(subject));
        if (text.Length > 0)
        {
            prompt.Append("Description: ").AppendLine(text.Length > DescriptionMaxChars ? text[..DescriptionMaxChars] : text);
        }

        prompt.AppendLine().AppendLine("Categories:");
        foreach (var category in active)
        {
            prompt.Append("- ").Append(category.Id).Append(": ").AppendLine(category.Name);
        }

        return prompt.ToString().TrimEnd();
    }

    private AiClassificationOutcome? Parse(string answer, HashSet<Guid> activeIds)
    {
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(answer[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("priority", out var priorityElement) || priorityElement.ValueKind != JsonValueKind.String
                || !TicketValues.TryParsePriority(priorityElement.GetString(), out var priority)
                || !TryReadConfidence(root, out var confidence))
            {
                return null;
            }

            Guid? categoryId = null;
            if (root.TryGetProperty("categoryId", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.String
                && Guid.TryParse(categoryElement.GetString(), out var parsed) && activeIds.Contains(parsed))
            {
                categoryId = parsed;
            }

            return new AiClassificationOutcome(categoryId, priority, confidence, confidence >= options.ConfidenceThreshold);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadConfidence(JsonElement root, out double confidence)
    {
        confidence = 0;
        if (!root.TryGetProperty("confidence", out var element))
        {
            return false;
        }

        var ok = element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDouble(out confidence),
            JsonValueKind.String => double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out confidence),
            _ => false,
        };
        if (!ok || double.IsNaN(confidence))
        {
            return false;
        }

        confidence = Math.Clamp(confidence, 0, 1);
        return true;
    }
}
