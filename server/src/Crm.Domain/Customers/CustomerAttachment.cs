namespace Crm.Domain.Customers;

/// <summary>
/// A file attached to a customer. The bytes live in file storage under <see cref="StorageKey"/>, which is built from
/// the ids only (never from the uploaded name); <see cref="FileName"/> is the original name, shown and used on download.
/// </summary>
public sealed class CustomerAttachment
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StorageKeyMaxLength = 200;

    private CustomerAttachment()
    {
        // EF Core materializes attachments through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Size in bytes.</summary>
    public long Size { get; private set; }

    /// <summary>"customers/{customerId:N}/{id:N}".</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public Guid? UploadedById { get; private set; }

    public DateTime UploadedAt { get; private set; }

    public static CustomerAttachment Create(
        Guid customerId, string fileName, string contentType, long size, Guid? uploadedById, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        var id = Guid.NewGuid();
        return new CustomerAttachment
        {
            Id = id,
            CustomerId = customerId,
            FileName = fileName.Length > FileNameMaxLength ? fileName[^FileNameMaxLength..] : fileName,
            ContentType = contentType,
            Size = size,
            StorageKey = $"customers/{customerId:N}/{id:N}",
            UploadedById = uploadedById,
            UploadedAt = utcNow,
        };
    }
}
