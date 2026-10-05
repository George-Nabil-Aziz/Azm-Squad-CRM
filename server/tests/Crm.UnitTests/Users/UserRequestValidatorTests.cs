using Crm.Application.Users;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Users;

public class UserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateUserRequestValidator _update = new();
    private readonly ListUsersQueryValidator _list = new();

    private static CreateUserRequest ValidCreate() =>
        new("agent@crm.local", "Sara Agent", "Agent#Pass1", ["Agent"]);

    [Fact]
    public void ValidCreateRequest_HasNoErrors()
    {
        Assert.True(_create.Validate(ValidCreate()).IsValid);
    }

    [Theory]
    [InlineData(null, "Email")]
    [InlineData("not-an-email", "Email")]
    public void CreateRequest_WithBadEmail_ReportsEmail(string? email, string field)
    {
        var result = _create.Validate(ValidCreate() with { Email = email });

        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void CreateRequest_WithoutFullName_ReportsFullName(string? fullName)
    {
        var result = _create.Validate(ValidCreate() with { FullName = fullName });

        Assert.Contains(result.Errors, e => e.PropertyName == "FullName");
    }

    [Theory]
    [InlineData("Short#1")] // 7 characters
    [InlineData("alllower#1")]
    [InlineData("ALLUPPER#1")]
    [InlineData("NoDigits#x")]
    [InlineData("NoSymbol12")]
    public void CreateRequest_WithWeakPassword_ReportsPassword(string password)
    {
        var result = _create.Validate(ValidCreate() with { Password = password });

        var error = Assert.Single(result.Errors);
        Assert.Equal("Password", error.PropertyName);
    }

    [Fact]
    public void CreateRequest_WithoutRoles_ReportsRoles()
    {
        var result = _create.Validate(ValidCreate() with { Roles = [] });

        Assert.Contains(result.Errors, e => e.PropertyName == "Roles");
    }

    [Fact]
    public void CreateRequest_WithUnknownRole_ReportsRoles()
    {
        var result = _create.Validate(ValidCreate() with { Roles = ["Agent", "admin"] }); // role names are case-sensitive

        Assert.Contains(result.Errors, e => e.PropertyName == "Roles");
    }

    [Fact]
    public void CreateRequest_InArabic_HasArabicMessages()
    {
        var messages = UiCulture.Use("ar", () => _create
            .Validate(new CreateUserRequest("", "", "weak", []))
            .Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'الاسم الكامل' لا يجب أن يكون فارغاً.", messages);
        Assert.Contains("اختر دوراً واحداً على الأقل.", messages);
        Assert.Contains("يجب أن تتكون كلمة المرور من 8 أحرف على الأقل وتحتوي على حرف كبير وحرف صغير ورقم ورمز.", messages);
    }

    [Fact]
    public void UpdateRequest_ValidAndInvalid()
    {
        Assert.True(_update.Validate(new UpdateUserRequest("agent@crm.local", "Sara", ["Supervisor"])).IsValid);

        var result = _update.Validate(new UpdateUserRequest("bad", "", null));

        Assert.Equal(["Email", "FullName", "Roles"], result.Errors.Select(e => e.PropertyName).Distinct().Order());
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void ListQuery_PagingLimits(int? page, int? pageSize, bool valid)
    {
        Assert.Equal(valid, _list.Validate(new ListUsersQuery(null, page, pageSize)).IsValid);
    }
}
