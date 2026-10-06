using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.QuickReplies;
using FluentValidation;

namespace Crm.Application.QuickReplies;

/// <summary>Body of POST / PUT /api/quick-replies: title and body are required; <c>IsShared</c> (default false) needs <c>quick-replies.manage-shared</c>.</summary>
public sealed record QuickReplyRequest(string? Title, string? Shortcut, string? Body, bool? IsShared);

/// <summary>A quick reply as the API returns it. <c>IsMine</c> = the signed-in user owns it.</summary>
public sealed record QuickReplyResponse(
    Guid Id, string Title, string? Shortcut, string Body, bool IsShared, Guid OwnerId, string? OwnerName, bool IsMine,
    DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>GET /api/quick-replies query: <c>search</c> matches the title or the shortcut.</summary>
public sealed record ListQuickRepliesQuery(string? Search);

/// <summary>Body of POST /api/quick-replies/{id}/render.</summary>
public sealed record RenderQuickReplyRequest(Guid? TicketId);

/// <summary>The reply text with the ticket data filled in.</summary>
public sealed record RenderedQuickReplyResponse(string Text);

/// <summary>Quick reply storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IQuickReplyRepository
{
    void Add(QuickReply reply);

    void Remove(QuickReply reply);

    /// <summary>The tracked reply (any owner), or null.</summary>
    Task<QuickReply?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The user's personal replies plus all shared ones; <paramref name="search"/> matches title or shortcut; by title.</summary>
    Task<IReadOnlyList<QuickReplyResponse>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken);

    Task<QuickReplyResponse?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    /// <summary>Customer name, number and subject of the ticket (agent name empty), or null for an unknown ticket.</summary>
    Task<QuickReplyValues?> FindTicketValuesAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Quick replies. <c>ForbiddenException</c> 403 for creating / changing a shared reply without
/// <c>quick-replies.manage-shared</c>; <c>NotFoundException</c> 404 for an unknown reply or somebody else's personal one.
/// </summary>
public interface IQuickReplyService
{
    Task<IReadOnlyList<QuickReplyResponse>> ListAsync(ListQuickRepliesQuery query, CancellationToken cancellationToken);

    Task<QuickReplyResponse> CreateAsync(QuickReplyRequest request, CancellationToken cancellationToken);

    Task<QuickReplyResponse> UpdateAsync(Guid id, QuickReplyRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<RenderedQuickReplyResponse> RenderAsync(Guid id, RenderQuickReplyRequest request, CancellationToken cancellationToken);
}

public sealed class QuickReplyRequestValidator : AbstractValidator<QuickReplyRequest>
{
    public QuickReplyRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(_ => QuickReplyText.TitleRequired).MaximumLength(QuickReply.TitleMaxLength);
        RuleFor(x => x.Shortcut).MaximumLength(QuickReply.ShortcutMaxLength);
        RuleFor(x => x.Body).NotEmpty().WithMessage(_ => QuickReplyText.BodyRequired).MaximumLength(QuickReply.BodyMaxLength);
    }
}

public sealed class QuickReplyService(
    IQuickReplyRepository replies,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<QuickReplyRequest> validator) : IQuickReplyService
{
    public async Task<IReadOnlyList<QuickReplyResponse>> ListAsync(ListQuickRepliesQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return await replies.ListAsync(UserId(), search, cancellationToken);
    }

    public async Task<QuickReplyResponse> CreateAsync(QuickReplyRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var shared = request.IsShared == true;
        if (shared)
        {
            EnsureCanManageShared();
        }

        var reply = QuickReply.Create(UserId(), request.Title!, request.Shortcut, request.Body!, shared, Now());
        replies.Add(reply);
        await replies.SaveChangesAsync(cancellationToken);
        return await Get(reply.Id, cancellationToken);
    }

    public async Task<QuickReplyResponse> UpdateAsync(Guid id, QuickReplyRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var reply = await FindEditableAsync(id, cancellationToken);
        var shared = request.IsShared == true;
        if (shared && !reply.IsShared)
        {
            EnsureCanManageShared(); // making a reply shared is managing a shared reply
        }

        reply.Update(request.Title!, request.Shortcut, request.Body!, shared, Now());
        await replies.SaveChangesAsync(cancellationToken);
        return await Get(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        replies.Remove(await FindEditableAsync(id, cancellationToken));
        await replies.SaveChangesAsync(cancellationToken);
    }

    public async Task<RenderedQuickReplyResponse> RenderAsync(Guid id, RenderQuickReplyRequest request, CancellationToken cancellationToken)
    {
        var reply = await FindVisibleAsync(id, cancellationToken);
        var values = request.TicketId is { } ticketId ? await replies.FindTicketValuesAsync(ticketId, cancellationToken) : null;
        if (values is null)
        {
            throw new NotFoundException(QuickReplyText.TicketNotFound);
        }

        var agent = await replies.GetUserNameAsync(UserId(), cancellationToken) ?? string.Empty;
        return new RenderedQuickReplyResponse(QuickReplyTemplate.Render(reply.Body, values with { AgentName = agent }));
    }

    public static QuickReplyResponse ToResponse(QuickReply reply, Guid userId, string? ownerName) => new(
        reply.Id, reply.Title, reply.Shortcut, reply.Body, reply.IsShared, reply.OwnerId, ownerName, reply.OwnerId == userId,
        reply.CreatedAt, reply.UpdatedAt);

    /// <summary>A reply the user may see: their own or a shared one.</summary>
    private async Task<QuickReply> FindVisibleAsync(Guid id, CancellationToken cancellationToken) =>
        await replies.FindAsync(id, cancellationToken) is { } reply && (reply.IsShared || reply.OwnerId == UserId())
            ? reply
            : throw new NotFoundException(QuickReplyText.NotFound);

    /// <summary>A reply the user may change: their own personal one, or a shared one with the permission (else 403).</summary>
    private async Task<QuickReply> FindEditableAsync(Guid id, CancellationToken cancellationToken)
    {
        var reply = await FindVisibleAsync(id, cancellationToken);
        if (reply.IsShared)
        {
            EnsureCanManageShared(); // AC 3
        }

        return reply;
    }

    private async Task<QuickReplyResponse> Get(Guid id, CancellationToken cancellationToken) =>
        await replies.GetAsync(id, UserId(), cancellationToken) ?? throw new NotFoundException(QuickReplyText.NotFound);

    private void EnsureCanManageShared()
    {
        if (!currentUser.HasPermission(Permissions.QuickRepliesManageShared))
        {
            throw new ForbiddenException(QuickReplyText.SharedForbidden);
        }
    }

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedException(QuickReplyText.NotFound);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}

/// <summary>User-facing text of the quick replies feature, in the request language.</summary>
public static class QuickReplyText
{
    public static string TitleRequired => LocalizedText.Get("Enter a title.", "أدخل عنواناً.");

    public static string BodyRequired => LocalizedText.Get("Enter the reply text.", "أدخل نص الرد.");

    public static string NotFound => LocalizedText.Get("The quick reply was not found.", "الرد السريع غير موجود.");

    public static string TicketNotFound => LocalizedText.Get("The ticket was not found.", "التذكرة غير موجودة.");

    public static string SharedForbidden => LocalizedText.Get(
        "You may not create or change shared quick replies.",
        "لا يمكنك إنشاء الردود السريعة المشتركة أو تعديلها.");
}
