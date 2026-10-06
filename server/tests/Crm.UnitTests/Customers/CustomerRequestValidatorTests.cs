using Crm.Application.Customers;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Customers;

public class CustomerRequestValidatorTests
{
    private readonly CustomerRequestValidator _request = new();
    private readonly ListCustomersQueryValidator _list = new();

    [Theory]
    [InlineData("Nour Trading", null, null)]
    [InlineData("Nour Trading", "", "")]
    [InlineData("Nour Trading", "info@nour.example", "+966 50 123-4567")]
    [InlineData("نور للتجارة", "info@nour.example", "(050) 123 4567")]
    [InlineData("نور للتجارة", null, "٠٥٠١٢٣٤٥٦٧")] // Arabic-Indic digits are normalized (CRM-9)
    public void ValidRequest_HasNoErrors(string name, string? email, string? phone)
    {
        Assert.True(_request.Validate(new CustomerRequest(name, email, phone)).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_WithoutName_ReportsName(string? name)
    {
        var result = _request.Validate(new CustomerRequest(name, null, null));

        var error = Assert.Single(result.Errors);
        Assert.Equal("Name", error.PropertyName);
    }

    [Fact]
    public void Request_WithTooLongValues_ReportsEachField()
    {
        var result = _request.Validate(new CustomerRequest(
            new string('n', 201), new string('e', 252) + "@x.io", "+" + new string('1', 32)));

        Assert.Equal(["Email", "Name", "Phone"], result.Errors.Select(e => e.PropertyName).Distinct().Order());
    }

    [Fact]
    public void Request_WithInvalidEmail_ReportsEmail()
    {
        var result = _request.Validate(new CustomerRequest("Nour", "not-an-email", null));

        Assert.Equal("Email", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("050-12a-4567")]
    [InlineData("12345")] // too short
    [InlineData("++966501234567")]
    [InlineData("+9665012345678")] // one digit too many (CRM-9: a real number is required)
    public void Request_WithInvalidPhone_ReportsPhone(string phone)
    {
        var result = _request.Validate(new CustomerRequest("Nour", null, phone));

        Assert.Equal("Phone", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Request_InArabic_HasArabicMessages()
    {
        var messages = UiCulture.Use("ar", () => _request
            .Validate(new CustomerRequest("", null, "call me"))
            .Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'الاسم' لا يجب أن يكون فارغاً.", messages);
        Assert.Contains("أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567.", messages);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void ListQuery_PagingLimits(int? page, int? pageSize, bool valid)
    {
        Assert.Equal(valid, _list.Validate(new ListCustomersQuery(null, page, pageSize)).IsValid);
    }
}
