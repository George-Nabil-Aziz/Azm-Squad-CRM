using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Auth;

/// <summary>Bound from the <c>Jwt</c> configuration section. <c>SigningKey</c> is a secret (user-secrets / env var).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && Encoding.UTF8.GetByteCount(SigningKey) >= 32
        && AccessTokenLifetimeMinutes > 0;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
