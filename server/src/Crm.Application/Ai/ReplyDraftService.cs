using System.Text;
using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Application.Tickets;

namespace Crm.Application.Ai;

/// <summary>A knowledge base article a reply draft was based on.</summary>
public sealed record ReplyDraftSource(Guid Id, string Title);

/// <summary>A suggested reply (never sent): <c>Language</c> is "ar" or "en", <c>Articles</c> the articles it was based on.</summary>
public sealed record ReplyDraftResponse(string Draft, string Language, IReadOnlyList<ReplyDraftSource> Articles);

/// <summary>
/// AI reply drafts (CRM-51; needs <c>tickets.manage</c> — enforced by the API). The draft is only returned: nothing is sent or
/// saved. Failures: <c>NotFoundException</c> 404, <see cref="AiNotConfiguredException"/> 503, <see cref="AiFailedException"/> 502.
/// </summary>
public interface IReplyDraftService
{
    Task<ReplyDraftResponse> SuggestAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class ReplyDraftService(
    ITicketRepository tickets,
    ITicketMessageRepository messages,
    IKbRetriever knowledgeBase,
    IAiTextService ai) : IReplyDraftService
{
    public const int ArticleCount = 3;
    private const int ArticleBodyMaxChars = 1_500;
    private const int MaxTokens = 800;

    public async Task<ReplyDraftResponse> SuggestAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!ai.IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        var ticket = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        // The draft goes to the customer: internal notes are never part of what the AI sees.
        var thread = await messages.ListAsync(ticketId, includeInternal: false, cancellationToken);

        var lastCustomerMessage = thread.Where(m => m.Direction == "inbound").OrderBy(m => m.CreatedAt).LastOrDefault();
        var language = lastCustomerMessage is not null
            ? AiLanguage.Detect([lastCustomerMessage.Body], ticket.Description ?? ticket.Subject)
            : AiLanguage.Detect([ticket.Description], ticket.Subject);

        var query = string.Join(' ', new[] { ticket.Subject, lastCustomerMessage?.Body, ticket.Description }.Where(t => !string.IsNullOrWhiteSpace(t)));
        var articles = await knowledgeBase.FindAsync(query, ArticleCount, language, cancellationToken);

        var (transcript, _) = TicketTranscript.Build(ticket.Subject, ticket.Description, thread);
        var answer = await ai.CompleteAsync(
            new AiRequest(SystemPrompt(language), UserPrompt(transcript, articles), MaxTokens), cancellationToken);
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new AiFailedException("The AI answered with an empty draft.");
        }

        return new ReplyDraftResponse(answer.Trim(), language, [.. articles.Select(a => new ReplyDraftSource(a.Id, a.Title))]);
    }

    private static string SystemPrompt(string language) =>
        "You write reply drafts for a customer support agent, who reviews and sends them. " +
        $"Write the reply in {AiLanguage.Name(language)}, the language of the customer's last message. " +
        "Use the knowledge base articles when they apply. Never invent prices, dates, policies or promises; if information is " +
        "missing, ask the customer for it. Be polite and concise: a greeting, the answer, a short closing and the signature " +
        "\"[Agent name]\". Placeholders such as [email] and [phone] stand for removed personal data: do not repeat them. " +
        "Reply with the draft only.";

    private static string UserPrompt(string transcript, IReadOnlyList<KbRetrievedArticle> articles)
    {
        var prompt = new StringBuilder("Ticket thread:\n").AppendLine(transcript).AppendLine();
        if (articles.Count == 0)
        {
            return prompt.Append("No knowledge base article matched this ticket. Do not invent policies.").ToString();
        }

        prompt.AppendLine("Knowledge base articles:");
        for (var i = 0; i < articles.Count; i++)
        {
            var body = articles[i].Body;
            prompt.Append('[').Append(i + 1).Append("] ").AppendLine(articles[i].Title)
                .AppendLine(body.Length > ArticleBodyMaxChars ? body[..ArticleBodyMaxChars] : body).AppendLine();
        }

        return prompt.ToString().TrimEnd();
    }
}
