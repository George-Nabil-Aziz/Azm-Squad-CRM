using Crm.Application.Ai;
using Crm.Application.Auth;

namespace Crm.Api.Endpoints;

/// <summary>The AI helpers of the staff UI (CRM-50..53). No endpoint needs a key at startup: without one they answer 503.</summary>
public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        // The UI asks once whether to show the AI actions at all.
        app.MapGet("/api/ai/status", (IAiTextService ai) => Results.Ok(new AiStatusResponse(ai.IsConfigured)))
            .RequireAuthorization(Permissions.TicketsView)
            .WithName("GetAiStatus");

        // Reading a ticket's AI results needs tickets.view; asking the AI to generate something needs tickets.manage.
        var ticket = app.MapGroup("/api/tickets/{id:guid}").RequireAuthorization(Permissions.TicketsView);

        ticket.MapGet("/ai-summary", async (Guid id, ITicketSummaryService summaries, CancellationToken cancellationToken) =>
                Results.Ok(await summaries.GetAsync(id, cancellationToken)))
            .WithName("GetTicketAiSummary");

        ticket.MapPost("/ai-summary", async (Guid id, ITicketSummaryService summaries, CancellationToken cancellationToken) =>
                Results.Ok(await summaries.GenerateAsync(id, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsManage)
            .WithName("GenerateTicketAiSummary");

        return app;
    }
}

/// <summary>Whether an AI provider key is configured (<c>enabled</c>); when false the UI hides the AI actions.</summary>
public sealed record AiStatusResponse(bool Enabled);
