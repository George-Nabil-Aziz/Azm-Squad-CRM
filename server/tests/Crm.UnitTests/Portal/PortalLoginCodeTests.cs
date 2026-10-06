using Crm.Domain.Portal;

namespace Crm.UnitTests.Portal;

public class PortalLoginCodeTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private const string Hash = "hash-of-123456";

    private static PortalLoginCode Issue() => PortalLoginCode.Issue("nour@customer.example", Hash, Now);

    [Fact]
    public void Issue_ExpiresAfterTenMinutes()
    {
        var code = Issue();

        Assert.Equal(Now.AddMinutes(10), code.ExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(10), PortalLoginCode.Lifetime);
        Assert.Equal(0, code.Attempts);
        Assert.Null(code.ConsumedAt);
    }

    [Fact]
    public void Issue_StoresTheEmailInLowerCase() =>
        Assert.Equal("nour@customer.example", PortalLoginCode.Issue(" Nour@Customer.Example ", Hash, Now).Email);

    [Fact]
    public void TheCorrectCode_Verifies_AndIsUsedUp()
    {
        var code = Issue();

        Assert.Equal(PortalCodeResult.Ok, code.Verify(Hash, Now.AddMinutes(1)));
        Assert.Equal(Now.AddMinutes(1), code.ConsumedAt);
        Assert.Equal(PortalCodeResult.Used, code.Verify(Hash, Now.AddMinutes(2)));
    }

    [Fact]
    public void AWrongCode_IsRejected_AndCounted()
    {
        var code = Issue();

        Assert.Equal(PortalCodeResult.Wrong, code.Verify("other", Now));
        Assert.Equal(1, code.Attempts);
        Assert.Null(code.ConsumedAt);
        Assert.Equal(PortalCodeResult.Ok, code.Verify(Hash, Now));
    }

    [Fact]
    public void TheCode_IsStillValidJustBeforeTenMinutes_AndExpiredAtTenMinutes()
    {
        Assert.Equal(PortalCodeResult.Ok, Issue().Verify(Hash, Now.AddMinutes(10).AddTicks(-1)));
        Assert.Equal(PortalCodeResult.Expired, Issue().Verify(Hash, Now.AddMinutes(10)));
        Assert.Equal(PortalCodeResult.Expired, Issue().Verify(Hash, Now.AddHours(1)));
    }

    [Fact]
    public void AfterFiveWrongTries_EvenTheCorrectCodeIsRefused()
    {
        var code = Issue();
        for (var i = 0; i < PortalLoginCode.MaxAttempts; i++)
        {
            Assert.Equal(PortalCodeResult.Wrong, code.Verify("wrong", Now));
        }

        Assert.Equal(PortalCodeResult.TooManyAttempts, code.Verify(Hash, Now));
        Assert.Equal(PortalCodeResult.TooManyAttempts, code.Verify("wrong", Now));
    }

    [Fact]
    public void Invalidate_UsesTheCodeUp()
    {
        var code = Issue();

        code.Invalidate(Now);

        Assert.Equal(PortalCodeResult.Used, code.Verify(Hash, Now));
    }

    [Fact]
    public void Issue_WithANonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => PortalLoginCode.Issue("a@b.example", Hash, DateTime.Now));
}

public class PortalAccountTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_LinksTheCustomer_ByLowerCaseEmail()
    {
        var customerId = Guid.NewGuid();

        var account = PortalAccount.Create(customerId, " Nour@Customer.Example ", Now);

        Assert.Equal(customerId, account.CustomerId);
        Assert.Equal("nour@customer.example", account.Email);
        Assert.Null(account.LastLoginAt);
    }

    [Fact]
    public void RecordLogin_SetsTheTime()
    {
        var account = PortalAccount.Create(Guid.NewGuid(), "a@b.example", Now);

        account.RecordLogin(Now.AddDays(1));

        Assert.Equal(Now.AddDays(1), account.LastLoginAt);
    }
}
