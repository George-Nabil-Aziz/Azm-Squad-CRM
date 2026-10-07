using System.Security.Cryptography;
using System.Text;

namespace Crm.Domain.Integrations;

/// <summary>What an API key may do on the public API (/api/v1).</summary>
public static class ApiKeyScopes
{
    public const string TicketsRead = "tickets:read";
    public const string TicketsWrite = "tickets:write";
    public const string CustomersRead = "customers:read";
    public const string CustomersWrite = "customers:write";

    public static IReadOnlyList<string> All { get; } = [TicketsRead, TicketsWrite, CustomersRead, CustomersWrite];

    public static bool IsKnown(string? scope) => scope is not null && All.Contains(scope, StringComparer.Ordinal);
}

/// <summary>
/// A key external systems use on /api/v1 (CRM-58). Only the SHA-256 hash and the first characters (to recognise it in a list)
/// are stored; the key itself is shown once, when it is created.
/// </summary>
public sealed class ApiKey
{
    public const int NameMaxLength = 100;
    public const string Prefix = "crm_";
    private const int PrefixLength = 8;

    private ApiKey()
    {
        // EF Core materializes keys through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>First 8 characters of the key ("crm_Ab3x"), safe to display.</summary>
    public string KeyPrefix { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the key, lower-case hex (64 characters).</summary>
    public string KeyHash { get; private set; } = string.Empty;

    /// <summary>Comma separated <see cref="ApiKeyScopes"/>.</summary>
    public string Scopes { get; private set; } = string.Empty;

    public Guid? CreatedById { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public DateTime? LastUsedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public IReadOnlyList<string> ScopeList => Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Creates a key and returns it together with the plain text, which exists only in this return value.</summary>
    public static (ApiKey Key, string PlainKey) Generate(string name, IEnumerable<string> scopes, Guid? createdById, DateTime utcNow)
    {
        var plain = Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            KeyPrefix = plain[..PrefixLength],
            KeyHash = Hash(plain),
            Scopes = string.Join(',', scopes.Distinct(StringComparer.Ordinal)),
            CreatedById = createdById,
            CreatedAt = utcNow,
        };
        return (key, plain);
    }

    public static string Hash(string plainKey) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey)));

    public bool HasScope(string scope) => ScopeList.Contains(scope, StringComparer.Ordinal);

    public void Revoke(DateTime utcNow) => RevokedAt ??= utcNow;

    public void MarkUsed(DateTime utcNow) => LastUsedAt = utcNow;

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
