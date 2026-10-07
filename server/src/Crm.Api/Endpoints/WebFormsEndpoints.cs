using Crm.Application.WebForms;

namespace Crm.Api.Endpoints;

/// <summary>The public web form (CRM-55): anonymous, protected by captcha and a per-IP rate limit.</summary>
public static class WebFormsEndpoints
{
    public static IEndpointRouteBuilder MapWebFormsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public/web-forms").AllowAnonymous();

        group.MapGet("/config", (IWebFormService forms) => Results.Ok(forms.GetConfig()))
            .WithName("GetWebFormConfig");

        group.MapPost("", async (WebFormRequest request, HttpContext http, IWebFormService forms, CancellationToken cancellationToken) =>
            {
                var receipt = await forms.SubmitAsync(request, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                return Results.Created((string?)null, receipt);
            })
            .WithName("SubmitWebForm");

        return app;
    }
}
