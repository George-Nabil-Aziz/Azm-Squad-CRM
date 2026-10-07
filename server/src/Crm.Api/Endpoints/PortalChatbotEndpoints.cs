using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Ai;
using Crm.Application.Portal;

namespace Crm.Api.Endpoints;

/// <summary>The portal chatbot (CRM-54). Anyone may chat; a hand-off to a ticket needs a signed-in customer (their token is read when present).</summary>
public static class PortalChatbotEndpoints
{
    public static IEndpointRouteBuilder MapPortalChatbotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/chatbot").AllowAnonymous();

        group.MapGet("/status", (IAiTextService ai) => Results.Ok(new AiStatusResponse(ai.IsConfigured)))
            .WithName("GetPortalChatbotStatus");

        group.MapPost("/messages", async (ChatbotRequest request, ClaimsPrincipal user, IChatbotService chatbot,
                    CancellationToken cancellationToken) =>
            {
                Guid? customerId = user.Identity?.IsAuthenticated == true && user.IsInRole(PortalRoles.Customer)
                    ? PortalUser.CustomerId(user)
                    : null;
                return Results.Ok(await chatbot.ReplyAsync(customerId, request, cancellationToken));
            })
            .WithName("SendPortalChatbotMessage");

        return app;
    }
}
