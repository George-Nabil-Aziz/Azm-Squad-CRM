using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public class ContactValuesTests
{
    [Theory]
    [InlineData("+966501234567", "+966501234567")]
    [InlineData("+966 50 123 4567", "+966501234567")]
    [InlineData("0501234567", "+966501234567")] // no country code: Saudi Arabia
    [InlineData("(050) 123-4567", "+966501234567")]
    [InlineData("00966501234567", "+966501234567")]
    [InlineData("966501234567", "+966501234567")] // WhatsApp sends numbers without "+"
    [InlineData("٠٥٠١٢٣٤٥٦٧", "+966501234567")] // Arabic-Indic digits
    [InlineData("۰۵۰۱۲۳۴۵۶۷", "+966501234567")] // Eastern Arabic-Indic (Persian) digits
    [InlineData("+14155552671", "+14155552671")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    public void TryNormalizePhone_ValidNumber_ReturnsE164(string input, string expected)
    {
        Assert.True(ContactValues.TryNormalizePhone(input, out var e164));
        Assert.Equal(expected, e164);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("call me")]
    [InlineData("050-12a-4567")] // letters
    [InlineData("12345")] // too short
    [InlineData("++966501234567")]
    [InlineData("+9665012345678")] // one digit too many
    [InlineData("+966521234567")] // no such Saudi mobile range
    [InlineData("+966 50 123 4567 ext. 5")] // extensions are not stored
    [InlineData("+0123")]
    [InlineData("+966501234567+966501234567+966501234567")] // longer than 32 characters
    public void TryNormalizePhone_InvalidNumber_ReturnsFalse(string? input)
    {
        Assert.False(ContactValues.TryNormalizePhone(input, out var e164));
        Assert.Null(e164);
    }

    [Theory]
    [InlineData("phone", ContactType.Phone)]
    [InlineData("email", ContactType.Email)]
    [InlineData("whatsapp", ContactType.WhatsApp)]
    [InlineData("WhatsApp", ContactType.WhatsApp)]
    public void TryParseType_KnownName_ReturnsTheType(string name, ContactType expected)
    {
        Assert.True(ContactValues.TryParseType(name, out var type));
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fax")]
    [InlineData("1")] // enum numbers are not accepted
    public void TryParseType_UnknownName_ReturnsFalse(string? name)
    {
        Assert.False(ContactValues.TryParseType(name, out _));
    }

    [Fact]
    public void TypeName_IsTheApiName()
    {
        Assert.Equal(["phone", "email", "whatsapp"],
            new[] { ContactType.Phone, ContactType.Email, ContactType.WhatsApp }.Select(ContactValues.TypeName));
    }
}
