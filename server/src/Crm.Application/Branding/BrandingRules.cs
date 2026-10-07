using System.Text.RegularExpressions;

namespace Crm.Application.Branding;

/// <summary>What may be stored as branding (CRM-63): hex colours and small raster logos.</summary>
public static partial class BrandingRules
{
    /// <summary>2 MB (2 x 1024 x 1024 bytes), inclusive.</summary>
    public const long MaxLogoBytes = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();

    /// <summary>"#RGB" or "#RRGGBB" (surrounding spaces ignored).</summary>
    public static bool IsValidColor(string? color) => color is not null && ColorPattern().IsMatch(color.Trim());

    /// <summary>Trimmed, lower case ("#1A2B3C" becomes "#1a2b3c"). The colour must be valid.</summary>
    public static string NormalizeColor(string color) => color.Trim().ToLowerInvariant();

    /// <summary>True for an allowed raster extension (SVG is refused: it can carry scripts); <paramref name="contentType"/> is its type.</summary>
    public static bool TryGetContentType(string? fileName, out string contentType)
    {
        contentType = string.Empty;
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrEmpty(extension) || !ContentTypes.TryGetValue(extension, out var found))
        {
            return false;
        }

        contentType = found;
        return true;
    }

    /// <summary>True when the first bytes of the file are those of the claimed image type (the name alone proves nothing).</summary>
    public static bool MatchesSignature(string contentType, ReadOnlySpan<byte> header) => contentType switch
    {
        "image/png" => header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/jpeg" => header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        "image/gif" => header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8),
        "image/webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false,
    };
}
