using System.Security.Cryptography;
using Crm.Application.Settings;
using Microsoft.AspNetCore.DataProtection;

namespace Crm.Infrastructure.Settings;

/// <summary>Encrypts stored secrets with ASP.NET Data Protection (purpose <c>Crm.SystemSettings</c>).</summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Crm.SystemSettings");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string? Unprotect(string protectedText)
    {
        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch (CryptographicException)
        {
            return null; // the key ring changed or was lost: the secret reads as "not set"
        }
    }
}
