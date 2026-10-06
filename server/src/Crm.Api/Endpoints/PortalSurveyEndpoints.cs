using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Portal;

namespace Crm.Api.Endpoints;

/// <summary>Customer satisfaction surveys (CRM-44): the anonymous email link and the signed-in portal form.</summary>
public static class PortalSurveyEndpoints
{
    public static IEndpointRouteBuilder MapPortalSurveyEndpoints(this IEndpointRouteBuilder app)
    {
        // The emailed link: the unguessable token is the credential, so no sign-in is needed.
        var link = app.MapGroup("/api/portal/surveys").AllowAnonymous();
        link.MapGet("/{token}", async (string token, ISurveyService surveys, CancellationToken cancellationToken) =>
                Results.Ok(await surveys.GetByTokenAsync(token, cancellationToken)))
            .WithName("GetPortalSurvey");
        link.MapPost("/{token}", async (string token, SubmitFeedbackRequest request, ISurveyService surveys,
                    CancellationToken cancellationToken) =>
                Results.Ok(await surveys.SubmitByTokenAsync(token, request, cancellationToken)))
            .WithName("SubmitPortalSurvey");

        // In the portal: the signed-in customer's own ticket (another customer's ticket is 404).
        var own = app.MapGroup("/api/portal/tickets/{id:guid}/feedback").RequireAuthorization(PortalPolicies.Customer);
        own.MapGet("", async (Guid id, ClaimsPrincipal user, ISurveyService surveys, CancellationToken cancellationToken) =>
                Results.Ok(await surveys.GetForCustomerAsync(PortalUser.CustomerId(user), id, cancellationToken)))
            .WithName("GetPortalTicketFeedback");
        own.MapPost("", async (Guid id, SubmitFeedbackRequest request, ClaimsPrincipal user, ISurveyService surveys,
                    CancellationToken cancellationToken) =>
                Results.Ok(await surveys.SubmitForCustomerAsync(PortalUser.CustomerId(user), id, request, cancellationToken)))
            .WithName("SubmitPortalTicketFeedback");

        return app;
    }
}
