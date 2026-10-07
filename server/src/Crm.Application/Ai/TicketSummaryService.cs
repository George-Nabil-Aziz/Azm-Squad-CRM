using System.Text;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Tickets;
using Crm.Domain.Ai;
using Crm.Domain.Tickets;

namespace Crm.Application.Ai;

/// <summary>The saved AI summary of a ticket; every field is null while it has none. <c>Language</c> is "ar" or "en".</summary>
public sealed record TicketSummaryResponse(string? Text, string? Language, DateTime? GeneratedAt);

/// <summary>Summary storage (EF Core in Crm.Infrastructure).</summary>
public interface ITicketSummaryRepository
{
    /// <summary>The tracked summary of the ticket, or null.</summary>
    Task<TicketAiSummary?> FindAsync(Guid ticketId, CancellationToken cancellationToken);

    void Add(TicketAiSummary summary);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One-click ticket summaries (CRM-50; reading needs <c>tickets.view</c>, generating <c>tickets.manage</c> — enforced by the API).
/// Failures: <c>NotFoundException</c> 404, <see cref="AiNotConfiguredException"/> 503, <see cref="AiFailedException"/> 502
/// (nothing is saved, the previous summary stays).
/// </summary>
public interface ITicketSummaryService
{
    Task<TicketSummaryResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Summarizes the ticket thread in the ticket language and saves it, replacing the previous summary.</summary>
    Task<TicketSummaryResponse> GenerateAsync(Guid ticketId, CancellationToken cancellationToken);
}

/// <summary>The thread of a ticket as text for a prompt: masked (<see cref="PiiMasker"/>), without names, newest messages kept.</summary>
public static class TicketTranscript
{
    public const int MaxChars = 12_000;
    private const int DescriptionMaxChars = 2_000;

    /// <summary>The masked transcript and the ticket language (customer text first, the subject when it has no letters).</summary>
    public static (string Text, string Language) Build(
        string subject, string? description, IReadOnlyList<TicketMessageResponse> messages, int maxChars = MaxChars)
    {
        var customerTexts = new List<string?> { description };
        customerTexts.AddRange(messages.Where(m => m.Direction == "inbound").Select(m => m.Body));
        var language = AiLanguage.Detect(customerTexts, subject);

        var header = new StringBuilder().Append("Subject: ").AppendLine(PiiMasker.Mask(subject));
        if (!string.IsNullOrWhiteSpace(description))
        {
            var masked = PiiMasker.Mask(description);
            header.Append("Description: ").AppendLine(masked.Length > DescriptionMaxChars ? masked[..DescriptionMaxChars] : masked);
        }

        var budget = maxChars - header.Length;
        var lines = new List<string>();
        var omitted = false;
        foreach (var message in messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id))
        {
            var line = $"{Label(message)}: {PiiMasker.Mask(message.Body)}";
            if (line.Length + 1 > budget)
            {
                omitted = true;
                break;
            }

            budget -= line.Length + 1;
            lines.Add(line);
        }

        lines.Reverse();
        var text = new StringBuilder(header.ToString());
        if (omitted)
        {
            text.AppendLine("[earlier messages omitted]");
        }

        foreach (var line in lines)
        {
            text.AppendLine(line);
        }

        return (text.ToString().TrimEnd(), language);
    }

    private static string Label(TicketMessageResponse message) => message.Direction switch
    {
        "inbound" => "Customer",
        "internal" => "Internal note",
        _ => "Agent",
    };
}

public sealed class TicketSummaryService(
    ITicketRepository tickets,
    ITicketMessageRepository messages,
    ITicketSummaryRepository summaries,
    IAiTextService ai,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ITicketSummaryService
{
    private const int MaxTokens = 700;

    public async Task<TicketSummaryResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureTicketAsync(ticketId, cancellationToken);
        return ToResponse(await summaries.FindAsync(ticketId, cancellationToken));
    }

    public async Task<TicketSummaryResponse> GenerateAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!ai.IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        var ticket = await EnsureTicketAsync(ticketId, cancellationToken);
        var thread = await messages.ListAsync(ticketId, includeInternal: true, cancellationToken);
        var (transcript, language) = TicketTranscript.Build(ticket.Subject, ticket.Description, thread);

        var answer = await ai.CompleteAsync(new AiRequest(SystemPrompt(language), transcript, MaxTokens), cancellationToken);
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new AiFailedException("The AI answered with an empty summary.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var summary = await summaries.FindAsync(ticketId, cancellationToken);
        if (summary is null)
        {
            summary = TicketAiSummary.Create(ticketId, answer, language, currentUser.UserId, now);
            summaries.Add(summary);
        }
        else
        {
            summary.Replace(answer, language, currentUser.UserId, now);
        }

        await summaries.SaveChangesAsync(cancellationToken);
        return ToResponse(summary);
    }

    private static string SystemPrompt(string language) =>
        "You summarize customer support tickets for the agent who has to handle them. " +
        $"Write the summary in {AiLanguage.Name(language)}. Use at most six short bullet points: what the customer needs, " +
        "what was already done, and what is still open. Placeholders such as [email] and [phone] stand for removed personal " +
        "data: do not mention them. Reply with the summary only.";

    private async Task<Ticket> EnsureTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);

    private static TicketSummaryResponse ToResponse(TicketAiSummary? summary) =>
        summary is null ? new TicketSummaryResponse(null, null, null) : new TicketSummaryResponse(summary.Text, summary.Language, summary.GeneratedAt);
}
