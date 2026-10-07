using System.Security.Cryptography;
using System.Text;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.RateLimiting;
using Crm.Application.Common.Validation;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Application.WebForms;
using Crm.Domain.Chat;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Chat;

public sealed class ChatService(
    IChatSessionRepository sessions,
    IChatNotifier notifier,
    IAgentPresence presence,
    ICustomerService customers,
    ITicketService tickets,
    IValidator<StartChatRequest> startValidator,
    IRateLimiter rateLimiter,
    WebFormOptions limits,
    TimeProvider timeProvider) : IChatService
{
    public bool IsAvailable => presence.HasOnlineAgents;

    public async Task<StartChatResponse> StartAsync(StartChatRequest request, string? remoteIp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!rateLimiter.TryAcquire(
                $"chat:{remoteIp ?? "unknown"}", Math.Max(1, limits.RateLimitRequests),
                TimeSpan.FromSeconds(Math.Max(1, limits.RateLimitWindowSeconds)), out var retryAfter))
        {
            throw new RateLimitExceededException(retryAfter);
        }

        await startValidator.ValidateOrThrowAsync(request, cancellationToken);
        if (!presence.HasOnlineAgents)
        {
            throw new ConflictException(ChatText.NoAgentOnline);
        }

        var now = UtcNow();
        var token = NewToken();
        var session = ChatSession.Start(request.Name!, request.Email!, Hash(token), now);
        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            session.AddMessage(ChatSender.Visitor, session.VisitorName, request.Message, now);
        }

        sessions.Add(session);
        await sessions.SaveChangesAsync(cancellationToken);

        var response = ToResponse(session);
        await notifier.ChatStartedAsync(response, cancellationToken);
        return new StartChatResponse(response, token);
    }

    public async Task<bool> AuthorizeVisitorAsync(Guid sessionId, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || await sessions.FindAsync(sessionId, cancellationToken) is not { } session)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(session.VisitorTokenHash));
    }

    public async Task<ChatSessionResponse> AcceptAsync(Guid sessionId, Guid agentId, string agentName, CancellationToken cancellationToken)
    {
        var session = await FindAsync(sessionId, cancellationToken);
        if (session.Status == ChatStatus.Ended)
        {
            throw new ConflictException(ChatText.Ended);
        }

        if (session.Status != ChatStatus.Waiting)
        {
            throw new ConflictException(ChatText.AlreadyAccepted);
        }

        session.Accept(agentId, agentName, UtcNow());
        await sessions.SaveChangesAsync(cancellationToken);

        var response = ToResponse(session);
        await notifier.ChatAcceptedAsync(response, cancellationToken);
        return response;
    }

    public async Task<ChatMessageResponse> SendAsync(
        Guid sessionId, ChatParticipant sender, string? body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > ChatMessage.BodyMaxLength)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["body"] = [ChatText.BodyInvalid(ChatMessage.BodyMaxLength)] });
        }

        var session = await FindAsync(sessionId, cancellationToken);
        if (session.Status == ChatStatus.Ended)
        {
            throw new ConflictException(ChatText.Ended);
        }

        if (sender.Sender == ChatSender.Agent && (session.Status != ChatStatus.Active || session.AgentId != sender.AgentId))
        {
            throw new ForbiddenException(ChatText.NotYourChat);
        }

        var name = sender.Sender == ChatSender.Agent ? sender.AgentName ?? session.AgentName ?? string.Empty : session.VisitorName;
        var message = session.AddMessage(sender.Sender, name, body, UtcNow());
        await sessions.SaveChangesAsync(cancellationToken);

        var response = ToResponse(message);
        await notifier.MessageAsync(response, cancellationToken);
        return response;
    }

    public async Task<ChatSessionResponse> EndAsync(Guid sessionId, ChatParticipant by, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(by);
        var session = await FindAsync(sessionId, cancellationToken);
        if (session.Status == ChatStatus.Ended)
        {
            return ToResponse(session);
        }

        if (by.Sender == ChatSender.Agent && session.Status == ChatStatus.Active && session.AgentId != by.AgentId)
        {
            throw new ForbiddenException(ChatText.NotYourChat);
        }

        var ticket = await SaveTranscriptAsync(session, cancellationToken);
        session.End(UtcNow());
        session.LinkTicket(ticket.Id, ticket.Number);
        await sessions.SaveChangesAsync(cancellationToken);

        var response = ToResponse(session);
        await notifier.EndedAsync(response, cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<ChatSessionResponse>> ListAsync(string? status, Guid agentId, CancellationToken cancellationToken)
    {
        var waiting = !string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
        var found = waiting
            ? await sessions.ListAsync(ChatStatus.Waiting, null, cancellationToken)
            : await sessions.ListAsync(ChatStatus.Active, agentId, cancellationToken);
        return [.. found.Select(ToResponse)];
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> MessagesAsync(Guid sessionId, CancellationToken cancellationToken) =>
        [.. (await FindAsync(sessionId, cancellationToken)).Messages.OrderBy(m => m.SentAt).Select(ToResponse)];

    /// <summary>The customer is matched by the visitor's email (or created); the ticket holds the transcript.</summary>
    private async Task<TicketResponse> SaveTranscriptAsync(ChatSession session, CancellationToken cancellationToken)
    {
        var customer = (await customers.LookupAsync(new CustomerLookupQuery(null, session.VisitorEmail), cancellationToken)).FirstOrDefault()
                       ?? await customers.CreateAsync(new CustomerRequest(session.VisitorName, session.VisitorEmail, null), cancellationToken);
        return await tickets.CreateForCustomerAsync(
            customer.Id,
            new CreateTicketRequest(customer.Id, ChatText.TicketSubject(session.VisitorName), ChatTranscript.Build(session.Messages), null, null),
            TicketChannel.Chat,
            cancellationToken);
    }

    private async Task<ChatSession> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await sessions.FindAsync(id, cancellationToken) ?? throw new NotFoundException(ChatText.NotFound);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    internal static ChatSessionResponse ToResponse(ChatSession session) => new(
        session.Id, session.VisitorName, session.VisitorEmail, session.Status.ToString().ToLowerInvariant(),
        session.AgentId, session.AgentName, session.StartedAt, session.EndedAt, session.TicketNumber);

    internal static ChatMessageResponse ToResponse(ChatMessage message) => new(
        message.Id, message.SessionId, message.Sender.ToString().ToLowerInvariant(), message.SenderName, message.Body, message.SentAt);
}
