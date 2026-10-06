namespace Crm.Domain.Portal;

/// <summary>The outcome of checking a one-time code.</summary>
public enum PortalCodeResult
{
    Ok,
    Wrong,
    Expired,
    Used,
    TooManyAttempts,
}

/// <summary>
/// A one-time sign-in code emailed to a customer (CRM-40). Only a hash of the code is kept. It is valid for
/// <see cref="Lifetime"/> from <see cref="CreatedAt"/>, works once and is refused after <see cref="MaxAttempts"/> wrong
/// tries. Times are UTC and come from the caller.
/// </summary>
public sealed class PortalLoginCode
{
    public const int MaxAttempts = 5;
    public const int EmailMaxLength = 256;
    public const int HashMaxLength = 128;

    /// <summary>A code expires 10 minutes after it was issued.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private PortalLoginCode()
    {
        // EF Core materializes codes through this constructor.
    }

    public Guid Id { get; private set; }

    /// <summary>Trimmed, lower case.</summary>
    public string Email { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Wrong tries so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>When the code was used (or replaced by a newer one); null while it can still be used.</summary>
    public DateTime? ConsumedAt { get; private set; }

    public static PortalLoginCode Issue(string email, string codeHash, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        EnsureUtc(utcNow);
        return new PortalLoginCode
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            CodeHash = codeHash,
            CreatedAt = utcNow,
            ExpiresAt = utcNow + Lifetime,
        };
    }

    /// <summary>Checks the hash of an entered code. Ok uses the code up; Wrong counts an attempt.</summary>
    public PortalCodeResult Verify(string codeHash, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (ConsumedAt is not null)
        {
            return PortalCodeResult.Used;
        }

        if (utcNow >= ExpiresAt)
        {
            return PortalCodeResult.Expired;
        }

        if (Attempts >= MaxAttempts)
        {
            return PortalCodeResult.TooManyAttempts;
        }

        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(codeHash), System.Text.Encoding.UTF8.GetBytes(CodeHash)))
        {
            Attempts++;
            return PortalCodeResult.Wrong;
        }

        ConsumedAt = utcNow;
        return PortalCodeResult.Ok;
    }

    /// <summary>Uses the code up without a sign-in (a newer code replaces it).</summary>
    public void Invalidate(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        ConsumedAt ??= utcNow;
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
