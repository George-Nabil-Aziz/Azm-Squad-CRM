using System.Security.Cryptography;
using System.Text;
using Crm.Application.Channels.WhatsApp;

namespace Crm.UnitTests.Channels;

public class WhatsAppSignatureTests
{
    private const string Secret = "test-app-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"object\":\"whatsapp_business_account\"}");

    private static string Expected(byte[] body, string secret) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    [Fact]
    public void Compute_IsTheHexHmacSha256WithPrefix() =>
        Assert.Equal(Expected(Body, Secret), WhatsAppSignature.Compute(Body, Secret));

    [Fact]
    public void IsValid_WithTheRightSignature_IsTrue() =>
        Assert.True(WhatsAppSignature.IsValid(Body, Expected(Body, Secret), Secret));

    [Fact]
    public void IsValid_AcceptsUpperCaseHex() =>
        Assert.True(WhatsAppSignature.IsValid(Body, "sha256=" + Expected(Body, Secret)["sha256=".Length..].ToUpperInvariant(), Secret));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("sha256=zz")]
    [InlineData("sha1=abcdef")]
    public void IsValid_WithAMissingOrMalformedHeader_IsFalse(string? header) =>
        Assert.False(WhatsAppSignature.IsValid(Body, header, Secret));

    [Fact]
    public void IsValid_WithAnotherSecret_IsFalse() =>
        Assert.False(WhatsAppSignature.IsValid(Body, Expected(Body, "other-secret"), Secret));

    [Fact]
    public void IsValid_WithATamperedBody_IsFalse() =>
        Assert.False(WhatsAppSignature.IsValid(Encoding.UTF8.GetBytes("{\"object\":\"x\"}"), Expected(Body, Secret), Secret));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_WithoutAConfiguredSecret_IsFalse(string? secret) =>
        Assert.False(WhatsAppSignature.IsValid(Body, Expected(Body, Secret), secret));
}
