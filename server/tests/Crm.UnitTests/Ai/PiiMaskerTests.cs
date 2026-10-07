using Crm.Application.Ai;

namespace Crm.UnitTests.Ai;

public class PiiMaskerTests
{
    [Theory]
    [InlineData("Write to sara.ali+vip@example.co.uk please", "Write to [email] please")]
    [InlineData("a@b.io and c@d.org", "[email] and [email]")]
    public void Emails_AreMasked(string text, string expected) => Assert.Equal(expected, PiiMasker.Mask(text));

    [Theory]
    [InlineData("Call +966 50 123 4567 now", "Call [phone] now")]
    [InlineData("Call 0501234567 now", "Call [phone] now")]
    [InlineData("Office (011) 234-5678.", "Office [phone].")]
    [InlineData("جوالي ٠٥٠١٢٣٤٥٦٧ شكرا", "جوالي [phone] شكرا")]
    [InlineData("+9665012345678", "[phone]")]
    public void PhoneNumbers_AreMasked(string text, string expected) => Assert.Equal(expected, PiiMasker.Mask(text));

    [Theory]
    [InlineData("Ticket TKT-000012 is open")]
    [InlineData("Order 12345 shipped on 2026-10-06")]
    [InlineData("It costs 1,250 SAR")]
    [InlineData("No personal data here")]
    public void OtherText_IsLeftAlone(string text) => Assert.Equal(text, PiiMasker.Mask(text));

    [Fact]
    public void NullOrBlank_GivesAnEmptyString()
    {
        Assert.Equal(string.Empty, PiiMasker.Mask(null));
        Assert.Equal(string.Empty, PiiMasker.Mask("  "));
    }

    [Fact]
    public void EmailAndPhoneTogether_BothMasked() =>
        Assert.Equal("Me: [email] / [phone]", PiiMasker.Mask("Me: nour@corp.example / +44 20 7946 0958"));
}
