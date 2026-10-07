using Crm.Application.Common.Localization;
using Crm.Domain.Chat;
using FluentValidation;

namespace Crm.Application.Chat;

/// <summary>Body of POST /api/public/chat/sessions: who the visitor is and an optional first message.</summary>
public sealed record StartChatRequest(string? Name, string? Email, string? Message);

/// <summary>A chat as clients see it. <c>Status</c>: "waiting" | "active" | "ended"; <c>TicketNumber</c> once the transcript was saved.</summary>
public sealed record ChatSessionResponse(
    Guid Id,
    string VisitorName,
    string VisitorEmail,
    string Status,
    Guid? AgentId,
    string? AgentName,
    DateTime StartedAt,
    DateTime? EndedAt,
    string? TicketNumber);

/// <summary><c>Sender</c>: "visitor" | "agent".</summary>
public sealed record ChatMessageResponse(Guid Id, Guid SessionId, string Sender, string SenderName, string Body, DateTime SentAt);

/// <summary>The new chat and the secret the visitor uses to connect to it (shown once, never stored).</summary>
public sealed record StartChatResponse(ChatSessionResponse Session, string VisitorToken);

public sealed record ChatAvailabilityResponse(bool Available);

/// <summary>The one writing in a chat: the visitor (already authorised by the token) or an agent.</summary>
public sealed record ChatParticipant(ChatSender Sender, Guid? AgentId, string? AgentName);

public sealed class StartChatRequestValidator : AbstractValidator<StartChatRequest>
{
    public StartChatRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ChatSession.NameMaxLength).WithName(_ => ChatText.NameField);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(ChatSession.EmailMaxLength).EmailAddress().WithName(_ => ChatText.EmailField);
        RuleFor(x => x.Message).MaximumLength(ChatMessage.BodyMaxLength).WithName(_ => ChatText.MessageField);
    }
}

public static class ChatText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string MessageField => LocalizedText.Get("Message", "الرسالة");

    public static string NoAgentOnline => LocalizedText.Get(
        "No agent is online right now. Leave a message and we will answer by email.",
        "لا يوجد موظف متصل الآن. اترك رسالة وسنرد عليك بالبريد الإلكتروني.");

    public static string NotFound => LocalizedText.Get("The chat was not found.", "المحادثة غير موجودة.");

    public static string AlreadyAccepted => LocalizedText.Get(
        "Another agent already took this chat.",
        "موظف آخر استلم هذه المحادثة بالفعل.");

    public static string Ended => LocalizedText.Get("This chat has ended.", "انتهت هذه المحادثة.");

    public static string NotYourChat => LocalizedText.Get(
        "Only the agent who accepted the chat can write in it.",
        "فقط الموظف الذي استلم المحادثة يمكنه الكتابة فيها.");

    public static string BodyField => LocalizedText.Get("Message", "الرسالة");

    public static string BodyInvalid(int max) => LocalizedText.Get(
        $"Write a message of at most {max} characters.",
        $"اكتب رسالة لا تزيد عن {max} حرفاً.");

    public static string TicketSubject(string visitorName) => LocalizedText.Get(
        $"Chat with {visitorName}",
        $"محادثة مع {visitorName}");

    public static string NoMessages => LocalizedText.Get("(no messages)", "(لا توجد رسائل)");
}
