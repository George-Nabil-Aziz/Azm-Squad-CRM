using System.Globalization;
using System.Text;
using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.KnowledgeBase;
using Crm.Application.Tickets;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Ai;

/// <summary>One chat message: <c>Role</c> is "user" (the customer) or "assistant" (the chatbot).</summary>
public sealed record ChatbotMessage(string? Role, string? Content);

/// <summary>Body of POST /api/portal/chatbot/messages: the transcript so far (the last message is the customer's) and an explicit hand-off request.</summary>
public sealed record ChatbotRequest(IReadOnlyList<ChatbotMessage>? Messages, bool? Handoff);

/// <summary>A published article an answer is based on.</summary>
public sealed record ChatbotSource(Guid Id, string Title);

/// <summary>The ticket a hand-off created; <c>Number</c> is "TKT-000001".</summary>
public sealed record ChatbotTicket(Guid Id, string Number);

/// <summary>
/// The chatbot's reply. <c>Outcome</c>: "answered" (<c>Sources</c> cite the articles), "unknown" (it does not know; <c>OfferAgent</c> is true)
/// or "handoff" (a <c>Ticket</c> was created, or <c>SignInRequired</c> when the visitor is not signed in). <c>Language</c> is "ar" or "en".
/// </summary>
public sealed record ChatbotReply(
    string Answer, string Language, IReadOnlyList<ChatbotSource> Sources, string Outcome, bool OfferAgent, bool SignInRequired, ChatbotTicket? Ticket);

/// <summary>
/// The customer chatbot (CRM-54): answers only from published knowledge base articles, says when it does not know, and hands over to a
/// human by creating a ticket with the transcript. Stateless: the client sends the transcript. Failures: <c>ValidationException</c> 400 on
/// <c>messages</c>, <see cref="AiNotConfiguredException"/> 503, <see cref="AiFailedException"/> 502.
/// </summary>
public interface IChatbotService
{
    /// <param name="customerId">The signed-in portal customer, or null for a visitor (who cannot be handed over to a ticket).</param>
    Task<ChatbotReply> ReplyAsync(Guid? customerId, ChatbotRequest request, CancellationToken cancellationToken);
}

public sealed class ChatbotService(IKbRetriever knowledgeBase, IAiTextService ai, ITicketService tickets, AiOptions options) : IChatbotService
{
    public const int MaxMessages = 20;
    public const int MaxMessageChars = 2_000;
    private const int ArticleCount = 4;
    private const int ArticleBodyMaxChars = 1_500;
    private const string OmittedMarker = "[earlier messages omitted]";

    private static readonly string[] EnglishAgentPhrases =
    [
        "human", "agent", "agents", "representative", "operator", "real person", "live person", "customer service",
        "talk to someone", "speak to someone", "speak with someone",
    ];

    private static readonly string[] ArabicAgentPhrases =
    [
        "موظف", "بشري", "شخص حقيقي", "ممثل خدمه", "خدمه العملاء", "مندوب", "اكلم احد", "اتكلم مع احد", "اتحدث مع احد", "احد من الفريق",
    ];

    public async Task<ChatbotReply> ReplyAsync(Guid? customerId, ChatbotRequest request, CancellationToken cancellationToken)
    {
        var messages = Validate(request);
        if (!ai.IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        var userTexts = messages.Where(m => m.Role == "user").Select(m => m.Content!.Trim()).ToList();
        var last = userTexts[^1];
        var language = AiLanguage.Detect([last], userTexts.Count > 1 ? userTexts[^2] : null);

        if (request.Handoff == true || AsksForAnAgent(last))
        {
            return await HandoffAsync(customerId, messages, language, cancellationToken);
        }

        var query = string.Join(' ', userTexts.TakeLast(2));
        var articles = await knowledgeBase.FindAsync(query, ArticleCount, language, cancellationToken);
        if (articles.Count == 0)
        {
            return Unknown(language);
        }

        var answer = await ai.CompleteAsync(new AiRequest(SystemPrompt(language), UserPrompt(messages, articles), 700), cancellationToken);
        var parsed = Parse(answer);
        var sources = parsed.ArticleIds
            .Select(id => articles.FirstOrDefault(a => a.Id == id))
            .OfType<KbRetrievedArticle>().Distinct()
            .Select(a => new ChatbotSource(a.Id, a.Title)).ToList();
        if (!parsed.CanAnswer || string.IsNullOrWhiteSpace(parsed.Answer) || sources.Count == 0)
        {
            return Unknown(language);
        }

        if (parsed.Confidence < options.ConfidenceThreshold)
        {
            return await HandoffAsync(customerId, messages, language, cancellationToken);
        }

        return new ChatbotReply(parsed.Answer.Trim(), language, sources, "answered", false, false, null);
    }

    private static List<ChatbotMessage> Validate(ChatbotRequest request)
    {
        var messages = request.Messages;
        if (messages is not { Count: > 0 } || messages.Count > MaxMessages
            || messages.Any(m => m.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(m.Content) || m.Content.Length > MaxMessageChars)
            || messages[^1].Role != "user")
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["messages"] = [AiText.ChatMessagesInvalid(MaxMessages, MaxMessageChars)] });
        }

        return [.. messages];
    }

    private static bool AsksForAnAgent(string text)
    {
        var arabic = KbSearchText.Normalize(text);
        if (ArabicAgentPhrases.Any(phrase => arabic.Contains(phrase, StringComparison.Ordinal)))
        {
            return true;
        }

        var english = " " + string.Join(' ', new string([.. text.ToLowerInvariant().Select(c => char.IsAsciiLetter(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";
        return EnglishAgentPhrases.Any(phrase => english.Contains($" {phrase} ", StringComparison.Ordinal));
    }

    private static ChatbotReply Unknown(string language) =>
        new(ChatbotText.Unknown(language), language, [], "unknown", true, false, null);

    private async Task<ChatbotReply> HandoffAsync(
        Guid? customerId, IReadOnlyList<ChatbotMessage> messages, string language, CancellationToken cancellationToken)
    {
        if (customerId is not { } id)
        {
            return new ChatbotReply(ChatbotText.SignInRequired(language), language, [], "handoff", false, true, null);
        }

        var firstQuestion = messages.First(m => m.Role == "user").Content!.Trim().ReplaceLineEndings(" ");
        var subject = firstQuestion.Length > Ticket.SubjectMaxLength ? firstQuestion[..Ticket.SubjectMaxLength] : firstQuestion;
        var created = await tickets.CreateForCustomerAsync(
            id, new CreateTicketRequest(id, subject, Transcript(messages), null, null), TicketChannel.Portal, cancellationToken);
        return new ChatbotReply(
            ChatbotText.HandedOver(language, created.Number), language, [], "handoff", false, false, new ChatbotTicket(created.Id, created.Number));
    }

    /// <summary>The full chat as "Customer: …" / "Chatbot: …" lines; the oldest messages are left out (with a marker) above the ticket's size limit.</summary>
    private static string Transcript(IReadOnlyList<ChatbotMessage> messages)
    {
        var lines = messages.Select(m => $"{(m.Role == "user" ? "Customer" : "Chatbot")}: {m.Content!.Trim()}").ToList();
        if (string.Join('\n', lines).Length <= Ticket.DescriptionMaxLength)
        {
            return string.Join('\n', lines);
        }

        var kept = new List<string>();
        var budget = Ticket.DescriptionMaxLength - OmittedMarker.Length - 1;
        foreach (var line in Enumerable.Reverse(lines))
        {
            if (line.Length + 1 > budget)
            {
                break;
            }

            budget -= line.Length + 1;
            kept.Insert(0, line);
        }

        return OmittedMarker + "\n" + string.Join('\n', kept);
    }

    private static string SystemPrompt(string language) =>
        "You are the help chatbot of a customer support portal. Answer ONLY with information from the help articles you are given; " +
        "never use other knowledge and never invent facts. " +
        $"Write the answer in {AiLanguage.Name(language)}. Reply with one JSON object only: " +
        "{\"canAnswer\": true | false, \"answer\": \"<short answer>\", \"articleIds\": [\"<ids of the articles you used>\"], \"confidence\": <number from 0 to 1>}. " +
        "If the articles do not answer the question, set canAnswer to false. Placeholders such as [email] and [phone] stand for removed personal data.";

    private static string UserPrompt(IReadOnlyList<ChatbotMessage> messages, IReadOnlyList<KbRetrievedArticle> articles)
    {
        var prompt = new StringBuilder("Conversation so far:\n");
        foreach (var message in messages)
        {
            prompt.Append(message.Role == "user" ? "Customer: " : "Assistant: ").AppendLine(PiiMasker.Mask(message.Content));
        }

        prompt.AppendLine().AppendLine("Help articles (the only allowed source):");
        foreach (var article in articles)
        {
            prompt.Append('[').Append(article.Id).Append("] ").AppendLine(article.Title)
                .AppendLine(article.Body.Length > ArticleBodyMaxChars ? article.Body[..ArticleBodyMaxChars] : article.Body).AppendLine();
        }

        return prompt.ToString().TrimEnd();
    }

    private sealed record ParsedAnswer(bool CanAnswer, string Answer, IReadOnlyList<Guid> ArticleIds, double Confidence);

    private static ParsedAnswer Parse(string answer)
    {
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new AiFailedException("The AI answer could not be read.");
        }

        try
        {
            using var document = JsonDocument.Parse(answer[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("canAnswer", out var can)
                || can.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new AiFailedException("The AI answer could not be read.");
            }

            var text = root.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() ?? string.Empty : string.Empty;
            var ids = new List<Guid>();
            if (root.TryGetProperty("articleIds", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                ids.AddRange(array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => Guid.TryParse(e.GetString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty));
            }

            var confidence = 0.0;
            if (root.TryGetProperty("confidence", out var c))
            {
                confidence = c.ValueKind switch
                {
                    JsonValueKind.Number => c.GetDouble(),
                    JsonValueKind.String when double.TryParse(c.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => 0.0,
                };
            }

            return new ParsedAnswer(can.GetBoolean(), text, ids, Math.Clamp(confidence, 0, 1));
        }
        catch (JsonException exception)
        {
            throw new AiFailedException("The AI answer could not be read.", exception);
        }
    }
}

/// <summary>Fixed chatbot messages, in the language of the customer's last message ("ar" or "en") rather than the request language.</summary>
public static class ChatbotText
{
    public static string Unknown(string language) => language == LocalizedText.Arabic
        ? "لم أجد إجابة عن هذا السؤال في مقالات المساعدة. هل تريد التحدث مع أحد موظفينا؟"
        : "I could not find this in our help articles. Would you like to talk to one of our agents?";

    public static string SignInRequired(string language) => language == LocalizedText.Arabic
        ? "لتحويلك إلى أحد موظفينا يرجى تسجيل الدخول أولاً، وسننشئ طلباً يتضمن هذه المحادثة."
        : "To connect you with an agent, please sign in first. We will create a request with this conversation.";

    public static string HandedOver(string language, string number) => language == LocalizedText.Arabic
        ? $"حوّلتك إلى فريق الدعم. أنشأنا الطلب {number} ويتضمن المحادثة كاملة، وسيتواصل معك أحد الموظفين."
        : $"I have passed this to our support team. Request {number} was created with the whole conversation, and an agent will get back to you.";
}
