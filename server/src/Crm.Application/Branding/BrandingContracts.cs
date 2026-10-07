namespace Crm.Application.Branding;

/// <summary>
/// The branding the staff app, the portal and the emails use (CRM-63). Null colours / logo = the default theme.
/// <c>LogoUrl</c> is a relative URL whose <c>v</c> changes with every new logo (cache busting).
/// </summary>
public sealed record BrandingResponse(string? PrimaryColor, string? SecondaryColor, string? LogoUrl);

/// <summary>Body of PUT /api/branding: "#RGB" / "#RRGGBB"; null or empty = back to the default colour.</summary>
public sealed record UpdateBrandingRequest(string? PrimaryColor, string? SecondaryColor);

/// <summary>The uploaded logo file (multipart field "file").</summary>
public sealed record UploadLogoRequest(string? FileName, long Length, Stream? Content);

/// <summary>The stored logo; the caller disposes <see cref="Content"/>.</summary>
public sealed record BrandingLogo(Stream Content, string ContentType);

/// <summary>What an email needs of the branding: colours and the logo bytes (to embed).</summary>
public sealed record EmailBranding(string? PrimaryColor, string? SecondaryColor, byte[]? Logo, string? LogoContentType);

/// <summary>
/// Custom branding. Failures: <c>ValidationException</c> 400 (invalid colour on <c>primaryColor</c> / <c>secondaryColor</c>,
/// bad logo on <c>file</c>). Reading is public; the API lets only <c>settings.manage</c> change it.
/// </summary>
public interface IBrandingService
{
    Task<BrandingResponse> GetAsync(CancellationToken cancellationToken);

    Task<BrandingResponse> UpdateAsync(UpdateBrandingRequest request, CancellationToken cancellationToken);

    Task<BrandingResponse> UploadLogoAsync(UploadLogoRequest request, CancellationToken cancellationToken);

    Task<BrandingResponse> RemoveLogoAsync(CancellationToken cancellationToken);

    /// <summary>The logo, or null when there is none.</summary>
    Task<BrandingLogo?> OpenLogoAsync(CancellationToken cancellationToken);

    Task<EmailBranding> GetEmailBrandingAsync(CancellationToken cancellationToken);
}
