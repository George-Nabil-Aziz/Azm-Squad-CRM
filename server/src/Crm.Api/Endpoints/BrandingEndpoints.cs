using Crm.Application.Auth;
using Crm.Application.Branding;

namespace Crm.Api.Endpoints;

public static class BrandingEndpoints
{
    public static IEndpointRouteBuilder MapBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/branding");

        // Public: the login page and the customer portal need the branding before anyone signs in.
        group.MapGet("", async (IBrandingService branding, CancellationToken cancellationToken) =>
                Results.Ok(await branding.GetAsync(cancellationToken)))
            .AllowAnonymous()
            .WithName("GetBranding");

        // The logo bytes. The URL carries a version (?v=), so the browser may cache it; the file is never run as a document.
        group.MapGet("/logo", async (IBrandingService branding, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                if (await branding.OpenLogoAsync(cancellationToken) is not { } logo)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
                httpContext.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'";
                httpContext.Response.Headers.CacheControl = "public, max-age=3600";
                return Results.File(logo.Content, logo.ContentType);
            })
            .AllowAnonymous()
            .WithName("GetBrandingLogo");

        // Changing the branding: SuperAdmin only (settings.manage). 401 without a token, 403 for every other role.
        group.MapPut("", async (UpdateBrandingRequest request, IBrandingService branding, CancellationToken cancellationToken) =>
                Results.Ok(await branding.UpdateAsync(request, cancellationToken)))
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("UpdateBranding");

        // Multipart upload, field "file". Size, type and content are checked by the Application layer (400 on "file").
        // No antiforgery: the API authenticates with a bearer header, not cookies, so CSRF does not apply.
        group.MapPut("/logo", async (IFormFile? file, IBrandingService branding, CancellationToken cancellationToken) =>
            {
                await using var content = file?.OpenReadStream();
                return Results.Ok(await branding.UploadLogoAsync(
                    new UploadLogoRequest(file?.FileName, file?.Length ?? 0, content), cancellationToken));
            })
            .DisableAntiforgery()
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("UploadBrandingLogo");

        group.MapDelete("/logo", async (IBrandingService branding, CancellationToken cancellationToken) =>
                Results.Ok(await branding.RemoveLogoAsync(cancellationToken)))
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("RemoveBrandingLogo");

        return app;
    }
}
