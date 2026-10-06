using Crm.Application.Customers;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Customers;

public class CustomerContactValidatorTests
{
    private readonly CustomerContactRequestValidator _contact = new();
    private readonly CustomerLookupQueryValidator _lookup = new();

    [Theory]
    [InlineData("phone", "+966501234567")]
    [InlineData("phone", "050 123 4567")]
    [InlineData("whatsapp", "٠٥٠١٢٣٤٥٦٧")]
    [InlineData("email", "sales@nour.example")]
    public void ValidContact_HasNoErrors(string type, string value)
    {
        Assert.True(_contact.Validate(new CustomerContactRequest(type, value, null)).IsValid);
    }

    [Theory]
    [InlineData("phone", "12345")]
    [InlineData("phone", "call me")]
    [InlineData("whatsapp", "+9665012345678")]
    [InlineData("email", "not-an-email")]
    [InlineData("phone", "")]
    [InlineData("email", null)]
    public void InvalidPhoneOrEmail_ReportsValue(string type, string? value)
    {
        var result = _contact.Validate(new CustomerContactRequest(type, value, true));

        Assert.Equal("Value", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void TooLongEmail_ReportsValue()
    {
        var result = _contact.Validate(new CustomerContactRequest("email", new string('e', 252) + "@x.io", null));

        Assert.Equal("Value", result.Errors.Select(e => e.PropertyName).Distinct().Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fax")]
    public void UnknownType_ReportsType(string? type)
    {
        var result = _contact.Validate(new CustomerContactRequest(type, "+966501234567", null));

        Assert.Equal("Type", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void InvalidContact_InArabic_HasArabicMessages()
    {
        var messages = UiCulture.Use("ar", () => _contact
            .Validate(new CustomerContactRequest("phone", "12345", null))
            .Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Equal(["أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567."], messages);
    }

    [Theory]
    [InlineData("+966501234567", null)]
    [InlineData("0501234567", "")]
    [InlineData(null, "info@nour.example")]
    public void ValidLookup_HasNoErrors(string? phone, string? email)
    {
        Assert.True(_lookup.Validate(new CustomerLookupQuery(phone, email)).IsValid);
    }

    [Theory]
    [InlineData(null, null, "Phone")] // neither
    [InlineData("  ", "", "Phone")]
    [InlineData("+966501234567", "info@nour.example", "Email")] // both
    [InlineData("12345", null, "Phone")]
    [InlineData(null, "not-an-email", "Email")]
    public void InvalidLookup_ReportsTheField(string? phone, string? email, string field)
    {
        var result = _lookup.Validate(new CustomerLookupQuery(phone, email));

        Assert.Equal(field, Assert.Single(result.Errors).PropertyName);
    }
}
