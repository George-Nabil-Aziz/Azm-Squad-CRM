namespace Crm.Domain.Customers;

/// <summary>A note an agent wrote about a customer (plain text). Written once; who wrote it and when are kept.</summary>
public sealed class CustomerNote
{
    public const int TextMaxLength = 4000;

    private CustomerNote()
    {
        // EF Core materializes notes through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>The staff user who wrote the note (null if written by the system).</summary>
    public Guid? AuthorId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static CustomerNote Create(Guid customerId, string text, Guid? authorId, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var trimmed = text.Trim();
        if (trimmed.Length > TextMaxLength)
        {
            throw new ArgumentException($"A note has at most {TextMaxLength} characters.", nameof(text));
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new CustomerNote { Id = Guid.NewGuid(), CustomerId = customerId, Text = trimmed, AuthorId = authorId, CreatedAt = utcNow };
    }
}
