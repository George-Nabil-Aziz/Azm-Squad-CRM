using Crm.Application.Auth;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(new LoginRequest("admin@crm.local", "Secret#123"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null, "Secret#123", "Email")]
    [InlineData("", "Secret#123", "Email")]
    [InlineData("not-an-email", "Secret#123", "Email")]
    [InlineData("admin@crm.local", null, "Password")]
    [InlineData("admin@crm.local", "", "Password")]
    public void InvalidRequest_ReportsTheField(string? email, string? password, string field)
    {
        var result = _validator.Validate(new LoginRequest(email, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void EmptyRequest_InEnglish_HasEnglishMessages()
    {
        var messages = UiCulture.Use("en", () =>
            _validator.Validate(new LoginRequest("", "")).Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'Email' must not be empty.", messages);
        Assert.Contains("'Password' must not be empty.", messages);
    }

    [Fact]
    public void EmptyRequest_InArabic_HasArabicMessagesAndFieldNames()
    {
        var messages = UiCulture.Use("ar", () =>
            _validator.Validate(new LoginRequest("", "")).Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'البريد الإلكتروني' لا يجب أن يكون فارغاً.", messages);
        Assert.Contains("'كلمة المرور' لا يجب أن يكون فارغاً.", messages);
    }
}
