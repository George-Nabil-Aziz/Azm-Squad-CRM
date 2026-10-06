namespace Crm.Application.Customers.Attachments;

/// <summary>
/// Which files may be uploaded: at most <see cref="MaxSizeBytes"/> and one of the allowed extensions. The content type
/// stored and sent on download comes from this list, never from the uploading browser.
/// </summary>
public static class AttachmentRules
{
    /// <summary>10 MB (10 × 1024 × 1024 bytes), inclusive.</summary>
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    /// <summary>The allowed extensions, e.g. ".pdf" (lower case).</summary>
    public static IReadOnlyCollection<string> AllowedExtensions => ContentTypes.Keys;

    /// <summary>True when the file name ends with an allowed extension; <paramref name="contentType"/> is its type.</summary>
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

    /// <summary>The name without any folder part ("C:\x\a.pdf", "../a.pdf" → "a.pdf"), control characters removed, trimmed.</summary>
    public static string SafeFileName(string fileName)
    {
        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return new string([.. name.Where(c => !char.IsControl(c))]).Trim();
    }
}
