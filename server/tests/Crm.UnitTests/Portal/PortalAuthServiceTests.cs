using Crm.Application.Common.Exceptions;
using Crm.Application.Portal;
using Crm.Domain.Customers;
using Crm.UnitTests.Localization;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Portal;

public class PortalAuthServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private const string Email = "nour@customer.example";

    private readonly FakePortalAccounts _accounts = new();
    private readonly FakeCustomers _customers = new();
    private readonly RecordingSender _sender = new();
    private readonly FakeTokenGenerator _tokens = new();
    private readonly TestClock _clock = new(Start);

    private PortalAuthService Service(params string[] codes) => new(
        _accounts, _customers, _sender, _tokens, new FixedCodeGenerator(codes), new PortalOptions(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PortalAuthService>.Instance, _clock,
        new RequestCodeRequestValidator(), new VerifyCodeRequestValidator());

    private Customer AddCustomer(string name, string email)
    {
        var customer = Customer.Create(name, email, null, Start.UtcDateTime);
        _customers.Customers.Add(customer);
        return customer;
    }

    [Fact]
    public async Task RequestCode_MailsASixDigitCode_ToTheEmail()
    {
        await Service("123456").RequestCodeAsync(new RequestCodeRequest(" Nour@Customer.Example "), CancellationToken.None);

        var mail = Assert.Single(_sender.Sent);
        Assert.Equal(Email, mail.Recipient);
        Assert.Equal(Crm.Domain.Channels.ChannelKind.Email, mail.Channel);
        Assert.Contains("123456", mail.Body);
        Assert.Contains("10", mail.Body);
        Assert.NotEmpty(mail.Subject!);
        var stored = Assert.Single(_accounts.Codes);
        Assert.DoesNotContain("123456", stored.CodeHash); // only a hash is kept
        Assert.Equal(Start.UtcDateTime.AddMinutes(10), stored.ExpiresAt);
    }

    [Fact]
    public async Task RequestCode_ForAnUnknownEmail_StillSucceeds_WithoutRevealingAnything()
    {
        await Service("111111").RequestCodeAsync(new RequestCodeRequest("nobody@else.example"), CancellationToken.None);

        Assert.Single(_sender.Sent);
        Assert.Empty(_customers.Customers); // nothing is created before the code is entered
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task RequestCode_WithAnInvalidEmail_ThrowsValidation(string? email)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().RequestCodeAsync(new RequestCodeRequest(email), CancellationToken.None));

        Assert.Contains("email", error.Errors.Keys);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task RequestCode_Twice_WithinAMinute_SendsOnlyOneMail_ButLaterSendsANewOne()
    {
        var service = Service("111111", "222222");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);

        _clock.UtcNow = Start.AddSeconds(30);
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        Assert.Single(_sender.Sent);

        _clock.UtcNow = Start.AddSeconds(61);
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task ANewCode_ReplacesTheOldOne()
    {
        var service = Service("111111", "222222");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        _clock.UtcNow = Start.AddMinutes(2);
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "111111"), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_WithTheCorrectCode_SignsTheCustomerIn_AndCreatesTheLinkedAccount()
    {
        var existing = AddCustomer("Nour Trading", Email);
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);

        var response = await service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None);

        Assert.Equal($"token-{existing.Id}", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(existing.Id, response.Customer.Id);
        Assert.Equal("Nour Trading", response.Customer.Name);
        var subject = Assert.Single(_tokens.Subjects);
        Assert.Equal(["Customer"], subject.Roles);
        Assert.Equal(existing.Id, subject.UserId);
        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal(existing.Id, account.CustomerId); // AC 4: linked to the customer with the same email
        Assert.Equal(Start.UtcDateTime, account.LastLoginAt);
        Assert.Single(_customers.Customers);
    }

    [Fact]
    public async Task Verify_LinksToACustomerWhoseEmailIsASecondaryContact()
    {
        var customer = Customer.Create("Nour Trading", "main@customer.example", null, Start.UtcDateTime);
        customer.AddContact(ContactType.Email, Email, false, Start.UtcDateTime);
        _customers.Customers.Add(customer);
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);

        var response = await service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None);

        Assert.Equal(customer.Id, response.Customer.Id);
    }

    [Fact]
    public async Task Verify_ForAnUnknownEmail_CreatesTheCustomer()
    {
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest("sara.ali@new.example"), CancellationToken.None);

        var response = await service.VerifyAsync(new VerifyCodeRequest("sara.ali@new.example", "123456"), CancellationToken.None);

        var created = Assert.Single(_customers.Customers);
        Assert.Equal(created.Id, response.Customer.Id);
        Assert.Equal("sara.ali", created.Name);
        Assert.Equal("sara.ali@new.example", created.Email);
        Assert.Equal(created.Id, Assert.Single(_accounts.Accounts).CustomerId);
    }

    [Fact]
    public async Task Verify_AnExistingAccount_StaysLinked_AndRecordsTheLogin()
    {
        var customer = AddCustomer("Nour Trading", Email);
        var service = Service("111111", "222222");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        await service.VerifyAsync(new VerifyCodeRequest(Email, "111111"), CancellationToken.None);
        _clock.UtcNow = Start.AddDays(1);
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);

        await service.VerifyAsync(new VerifyCodeRequest(Email, "222222"), CancellationToken.None);

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal(customer.Id, account.CustomerId);
        Assert.Equal(Start.AddDays(1).UtcDateTime, account.LastLoginAt);
    }

    [Fact]
    public async Task Verify_WithAWrongCode_ThrowsUnauthorized()
    {
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "654321"), CancellationToken.None));

        Assert.Empty(_tokens.Subjects);
        Assert.Empty(_accounts.Accounts);
        Assert.Equal(1, _accounts.Codes[0].Attempts);
    }

    [Fact]
    public async Task Verify_AnExpiredCode_ThrowsUnauthorized()
    {
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        _clock.UtcNow = Start.AddMinutes(10);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_TheCodeWorksOnlyOnce()
    {
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        await service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_WithoutAnyRequestedCode_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None));

    [Fact]
    public async Task Verify_AfterFiveWrongTries_EvenTheRightCodeFails()
    {
        var service = Service("123456");
        await service.RequestCodeAsync(new RequestCodeRequest(Email), CancellationToken.None);
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => service.VerifyAsync(new VerifyCodeRequest(Email, "999999"), CancellationToken.None));
        }

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_WithAMissingCode_ThrowsValidation()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().VerifyAsync(new VerifyCodeRequest(Email, " "), CancellationToken.None));

        Assert.Contains("code", error.Errors.Keys);
    }

    [Fact]
    public async Task TheInvalidCodeMessage_IsLocalized()
    {
        var service = Service();

        var englishTask = UiCulture.Use("en", () => Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None)));
        var arabicTask = UiCulture.Use("ar", () => Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAsync(new VerifyCodeRequest(Email, "123456"), CancellationToken.None)));
        var english = (await englishTask).Message;
        var arabic = (await arabicTask).Message;

        Assert.NotEqual(english, arabic);
    }

    [Fact]
    public void TheCodeHash_DependsOnEmailAndCode_AndIgnoresCaseAndSpacesOfTheEmail()
    {
        Assert.Equal(PortalCodeHash.Compute("A@b.example", "123456"), PortalCodeHash.Compute(" a@B.example ", "123456"));
        Assert.NotEqual(PortalCodeHash.Compute("a@b.example", "123456"), PortalCodeHash.Compute("a@b.example", "123457"));
        Assert.NotEqual(PortalCodeHash.Compute("a@b.example", "123456"), PortalCodeHash.Compute("c@b.example", "123456"));
    }

    [Fact]
    public void TheRandomGenerator_ProducesSixDigits()
    {
        var generator = new RandomPortalCodeGenerator();
        for (var i = 0; i < 50; i++)
        {
            Assert.Matches("^[0-9]{6}$", generator.NewCode());
        }
    }
}
