using Crm.Api.Auth;
using Crm.Application.Auth;
using Crm.Application.Chat;
using Crm.Application.Common.Security;
using Crm.Application.WebForms;
using Crm.Domain.Tickets;

namespace Crm.Api.Endpoints;

/// <summary>Live chat REST (CRM-56): the public widget calls and the agents' lists. Real-time traffic goes through <c>/hubs/chat</c>.</summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var publicChat = app.MapGroup("/api/public/chat").AllowAnonymous();

        publicChat.MapGet("/availability", (IChatService chat) => Results.Ok(new ChatAvailabilityResponse(chat.IsAvailable)))
            .WithName("GetChatAvailability");

        publicChat.MapPost("/sessions", async (StartChatRequest request, HttpContext http, IChatService chat, CancellationToken cancellationToken) =>
            {
                var started = await chat.StartAsync(request, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                return Results.Created($"/api/public/chat/sessions/{started.Session.Id}", started);
            })
            .WithName("StartChat");

        // No agent online: the same form as the web form, the ticket gets the channel "chat".
        publicChat.MapPost("/offline", async (WebFormRequest request, HttpContext http, IWebFormService forms, CancellationToken cancellationToken) =>
            {
                var receipt = await forms.SubmitAsync(request, http.Connection.RemoteIpAddress?.ToString(), cancellationToken, TicketChannel.Chat);
                return Results.Created((string?)null, receipt);
            })
            .WithName("SubmitOfflineChatForm");

        var staff = app.MapGroup("/api/chat-sessions").RequireAuthorization(Permissions.ChatHandle);

        staff.MapGet("", async (string? status, ICurrentUser currentUser, IChatService chat, CancellationToken cancellationToken) =>
                Results.Ok(await chat.ListAsync(status, currentUser.UserId ?? Guid.Empty, cancellationToken)))
            .WithName("ListChatSessions");

        staff.MapGet("/{id:guid}/messages", async (Guid id, IChatService chat, CancellationToken cancellationToken) =>
                Results.Ok(await chat.MessagesAsync(id, cancellationToken)))
            .WithName("ListChatMessages");

        return app;
    }
}
