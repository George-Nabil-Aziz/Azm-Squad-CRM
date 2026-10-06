namespace Crm.Domain.Tickets;

/// <summary>
/// A file attached to a ticket (CRM-41: sent by the customer with the portal form). The bytes live in file storage under
/// <see cref="StorageKey"/>, built from ids only; <see cref="FileName"/> is the original name, used on download.
/// </summary>
public sealed class TicketAttachment
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StorageKeyMaxLength = 200;

    private TicketAttachment()
    {
        // EF Core materializes attachments through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Size in bytes.</summary>
    public long Size { get; private set; }

    /// <summary>"tickets/{ticketId:N}/{id:N}".</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public DateTime UploadedAt { get; private set; }

    public static TicketAttachment Create(Guid ticketId, string fileName, string contentType, long size, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        var id = Guid.NewGuid();
        return new TicketAttachment
        {
            Id = id,
            TicketId = ticketId,
            FileName = fileName.Length > FileNameMaxLength ? fileName[^FileNameMaxLength..] : fileName,
            ContentType = contentType,
            Size = size,
            StorageKey = $"tickets/{ticketId:N}/{id:N}",
            UploadedAt = utcNow,
        };
    }
}
