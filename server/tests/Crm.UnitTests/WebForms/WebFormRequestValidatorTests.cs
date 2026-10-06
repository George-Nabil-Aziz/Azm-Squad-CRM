using Crm.Application.WebForms;

namespace Crm.UnitTests.WebForms;

public class WebFormRequestValidatorTests
{
    private static WebFormRequest Valid() => new("Nour Ali", "nour@example.com", "Printer broken", "It prints blank pages.", "token", null);

    private static string[] FailedFields(WebFormRequest request) =>
        [.. new WebFormRequestValidator().Validate(request).Errors.Select(e => e.PropertyName)];

    [Fact]
    public void AValidRequest_PassesTheValidator() => Assert.Empty(FailedFields(Valid()));

    [Theory]
    [InlineData("Name")]
    [InlineData("Email")]
    [InlineData("Subject")]
    [InlineData("Message")]
    public void AMissingRequiredField_FailsOnThatField(string field)
    {
        var request = field switch
        {
            "Name" => Valid() with { Name = " " },
            "Email" => Valid() with { Email = null },
            "Subject" => Valid() with { Subject = "" },
            _ => Valid() with { Message = null },
        };

        Assert.Contains(field, FailedFields(request));
    }

    [Fact]
    public void AnInvalidEmail_Fails() => Assert.Contains("Email", FailedFields(Valid() with { Email = "not-an-email" }));

    [Fact]
    public void TooLongValues_Fail()
    {
        Assert.Contains("Name", FailedFields(Valid() with { Name = new string('a', 201) }));
        Assert.Contains("Subject", FailedFields(Valid() with { Subject = new string('a', 301) }));
        Assert.Contains("Message", FailedFields(Valid() with { Message = new string('a', 10_001) }));
    }
}
