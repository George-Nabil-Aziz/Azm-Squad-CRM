namespace Crm.Domain.Integrations;

public enum ErpSyncResult
{
    Success,
    Failed,
    NotConfigured,
}

/// <summary>One fetch of a customer's ERP data (CRM-60): what happened and when. Written for every sync, successful or not.</summary>
public sealed class ErpSyncLog
{
    public const int ErrorMaxLength = 500;

    private ErpSyncLog()
    {
        // EF Core materializes log rows through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string ErpCustomerId { get; private set; } = string.Empty;

    public ErpSyncResult Result { get; private set; }

    public string? Error { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static ErpSyncLog Create(Guid customerId, string erpCustomerId, ErpSyncResult result, string? error, DateTime utcNow) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        ErpCustomerId = erpCustomerId,
        Result = result,
        Error = error is { Length: > ErrorMaxLength } ? error[..ErrorMaxLength] : error,
        CreatedAt = utcNow,
    };
}
