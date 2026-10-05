using Crm.Application.Auth;

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
}
