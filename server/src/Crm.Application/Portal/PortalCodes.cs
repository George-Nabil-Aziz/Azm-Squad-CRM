using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crm.Application.Portal;

/// <summary>Creates the one-time codes (random 6 digits). Replaced by a fake in tests.</summary>
public interface IPortalCodeGenerator
{
    string NewCode();
}

public sealed class RandomPortalCodeGenerator : IPortalCodeGenerator
{
    public string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}

/// <summary>The stored form of a code: SHA-256 of "email:code" in hex (the code itself is never stored).</summary>
public static class PortalCodeHash
{
    public static string Compute(string email, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email.Trim().ToLowerInvariant()}:{code.Trim()}")));
}
