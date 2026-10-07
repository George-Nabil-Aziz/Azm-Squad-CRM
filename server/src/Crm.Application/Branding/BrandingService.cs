using Crm.Application.Audit;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Files;
using Crm.Application.Settings;
using Crm.Domain.Audit;
using Crm.Domain.Settings;

namespace Crm.Application.Branding;

/// <summary>
/// Branding kept in the system settings (keys <c>branding.*</c>, no secrets) and the logo file in <see cref="IFileStorage"/>
/// under "branding/{guid}"; a new logo gets a new key, so the key doubles as the cache-busting version.
/// </summary>
public sealed class BrandingService(
    ISettingsRepository settings,
    IFileStorage files,
    TimeProvider timeProvider,
    IAuditLogger audit) : IBrandingService
{
    private const string PrimaryKey = "branding.primary-color";
    private const string SecondaryKey = "branding.secondary-color";
    private const string LogoKey = "branding.logo-key";
    private const string LogoTypeKey = "branding.logo-type";
    private const string KeyPrefix = "branding/";

    public async Task<BrandingResponse> GetAsync(CancellationToken cancellationToken) =>
        ToResponse(await LoadAsync(cancellationToken));

    public async Task<BrandingResponse> UpdateAsync(UpdateBrandingRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var primary = CheckColor(request.PrimaryColor, "primaryColor", errors);
        var secondary = CheckColor(request.SecondaryColor, "secondaryColor", errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var stored = await LoadAsync(cancellationToken);
        var old = new { primaryColor = stored.Get(PrimaryKey), secondaryColor = stored.Get(SecondaryKey) };
        var now = Now();
        stored.Set(PrimaryKey, primary, now);
        stored.Set(SecondaryKey, secondary, now);
        await settings.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.BrandingUpdated, "Branding", null, old, new { primaryColor = primary, secondaryColor = secondary }),
            cancellationToken);
        return ToResponse(stored);
    }

    public async Task<BrandingResponse> UploadLogoAsync(UploadLogoRequest request, CancellationToken cancellationToken)
    {
        if (request.Content is null || request.Length <= 0)
        {
            throw FileError(BrandingText.FileRequired);
        }

        if (request.Length > BrandingRules.MaxLogoBytes)
        {
            throw FileError(BrandingText.FileTooLarge);
        }

        if (!BrandingRules.TryGetContentType(request.FileName, out var contentType))
        {
            throw FileError(BrandingText.FileTypeInvalid);
        }

        // The declared length is not trusted: the real content is measured and its first bytes must be those of the image type.
        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw FileError(BrandingText.FileRequired);
        }

        if (buffer.Length > BrandingRules.MaxLogoBytes)
        {
            throw FileError(BrandingText.FileTooLarge);
        }

        if (!BrandingRules.MatchesSignature(contentType, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16))))
        {
            throw FileError(BrandingText.FileTypeInvalid);
        }

        var stored = await LoadAsync(cancellationToken);
        var oldKey = stored.Get(LogoKey);
        var newKey = KeyPrefix + Guid.NewGuid().ToString("N");
        buffer.Position = 0;
        await files.SaveAsync(newKey, buffer, cancellationToken);

        var now = Now();
        stored.Set(LogoKey, newKey, now);
        stored.Set(LogoTypeKey, contentType, now);
        await settings.SaveChangesAsync(cancellationToken);
        if (oldKey is not null)
        {
            await files.DeleteAsync(oldKey, cancellationToken);
        }

        await audit.LogAsync(
            new AuditEvent(AuditActions.BrandingUpdated, "Branding", null, new { logo = oldKey is not null }, new { logo = true }),
            cancellationToken);
        return ToResponse(stored);
    }

    public async Task<BrandingResponse> RemoveLogoAsync(CancellationToken cancellationToken)
    {
        var stored = await LoadAsync(cancellationToken);
        if (stored.Get(LogoKey) is not { } key)
        {
            return ToResponse(stored);
        }

        var now = Now();
        stored.Set(LogoKey, null, now);
        stored.Set(LogoTypeKey, null, now);
        await settings.SaveChangesAsync(cancellationToken);
        await files.DeleteAsync(key, cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.BrandingUpdated, "Branding", null, new { logo = true }, new { logo = false }),
            cancellationToken);
        return ToResponse(stored);
    }

    public async Task<BrandingLogo?> OpenLogoAsync(CancellationToken cancellationToken)
    {
        var stored = await LoadAsync(cancellationToken);
        if (stored.Get(LogoKey) is not { } key || stored.Get(LogoTypeKey) is not { } type)
        {
            return null;
        }

        return await files.OpenReadAsync(key, cancellationToken) is { } content ? new BrandingLogo(content, type) : null;
    }

    public async Task<EmailBranding> GetEmailBrandingAsync(CancellationToken cancellationToken)
    {
        var stored = await LoadAsync(cancellationToken);
        byte[]? logo = null;
        if (stored.Get(LogoKey) is { } key && await files.OpenReadAsync(key, cancellationToken) is { } content)
        {
            await using (content)
            {
                using var copy = new MemoryStream();
                await content.CopyToAsync(copy, cancellationToken);
                logo = copy.ToArray();
            }
        }

        return new EmailBranding(stored.Get(PrimaryKey), stored.Get(SecondaryKey), logo, logo is null ? null : stored.Get(LogoTypeKey));
    }

    private static string? CheckColor(string? color, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        if (BrandingRules.IsValidColor(color))
        {
            return BrandingRules.NormalizeColor(color);
        }

        errors[field] = [BrandingText.ColorInvalid];
        return null;
    }

    private static ValidationException FileError(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<Stored> LoadAsync(CancellationToken cancellationToken) =>
        new(settings, [.. (await settings.ListAsync(cancellationToken)).Where(s => s.Key.StartsWith("branding.", StringComparison.Ordinal))]);

    private static BrandingResponse ToResponse(Stored stored) =>
        new(stored.Get(PrimaryKey), stored.Get(SecondaryKey),
            stored.Get(LogoKey) is { } key ? "/api/branding/logo?v=" + key[KeyPrefix.Length..] : null);

    /// <summary>The branding rows with get / set (a missing row is added when first set).</summary>
    private sealed class Stored(ISettingsRepository repository, List<SystemSetting> rows)
    {
        public string? Get(string key) => rows.FirstOrDefault(r => r.Key == key)?.Value;

        public void Set(string key, string? value, DateTime now)
        {
            if (rows.FirstOrDefault(r => r.Key == key) is { } row)
            {
                row.SetValue(value, now);
                return;
            }

            if (value is null)
            {
                return;
            }

            var created = SystemSetting.Create(key, false, value, now);
            rows.Add(created);
            repository.Add(created);
        }
    }
}
