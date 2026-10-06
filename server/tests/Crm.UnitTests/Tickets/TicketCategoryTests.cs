using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketCategoryTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsTheName_IsActive_AndSetsNormalizedName()
    {
        var category = TicketCategory.Create("  Billing issue ", Now);

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Billing issue", category.Name);
        Assert.Equal("BILLING ISSUE", category.NormalizedName);
        Assert.True(category.IsActive);
        Assert.Equal(Now, category.CreatedAt);
        Assert.Equal(Now, category.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithABlankName_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => TicketCategory.Create(name, Now));
    }

    [Fact]
    public void Create_WithANonUtcTime_Throws()
    {
        Assert.Throws<ArgumentException>(() => TicketCategory.Create("Billing", DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void Update_RenamesAndDeactivates_AndMovesUpdatedAt()
    {
        var category = TicketCategory.Create("Billing", Now);
        var later = Now.AddMinutes(5);

        category.Update(" Invoices ", isActive: false, later);

        Assert.Equal("Invoices", category.Name);
        Assert.Equal("INVOICES", category.NormalizedName);
        Assert.False(category.IsActive);
        Assert.Equal(Now, category.CreatedAt);
        Assert.Equal(later, category.UpdatedAt);
    }

    [Fact]
    public void NormalizeName_IgnoresCaseAndSurroundingSpaces()
    {
        Assert.Equal(TicketCategory.NormalizeName("billing"), TicketCategory.NormalizeName("  BILLING "));
    }
}
