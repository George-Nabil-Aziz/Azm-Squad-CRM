using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public class CustomerTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Created.AddHours(3);

    [Fact]
    public void Create_TrimsTheProfile_AndSetsIdAndTimestamps()
    {
        var customer = Customer.Create("  Nour Trading  ", " info@nour.example ", " +966 50 123 4567 ", Created);

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Nour Trading", customer.Name);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal("+966 50 123 4567", customer.Phone);
        Assert.Equal(Created, customer.CreatedAt);
        Assert.Equal(Created, customer.UpdatedAt);
        Assert.False(customer.IsDeleted);
        Assert.Null(customer.DeletedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutEmailOrPhone_StoresNull(string? missing)
    {
        var customer = Customer.Create("Walk-in", missing, missing, Created);

        Assert.Null(customer.Email);
        Assert.Null(customer.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(name, null, null, Created));
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws()
    {
        var local = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => Customer.Create("Nour", null, null, local));
    }

    [Fact]
    public void Update_ChangesTheProfileAndUpdatedAt_KeepsCreatedAt()
    {
        var customer = Customer.Create("Nour", "old@nour.example", "0501234567", Created);

        customer.Update(" Nour Trading Co. ", null, "+966501234567", Later);

        Assert.Equal("Nour Trading Co.", customer.Name);
        Assert.Null(customer.Email);
        Assert.Equal("+966501234567", customer.Phone);
        Assert.Equal(Created, customer.CreatedAt);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void Delete_IsSoft_TheCustomerKeepsItsData()
    {
        var customer = Customer.Create("Nour", "info@nour.example", null, Created);
        var id = customer.Id;

        customer.Delete(Later);

        Assert.True(customer.IsDeleted);
        Assert.Equal(Later, customer.DeletedAt);
        Assert.Equal(Later, customer.UpdatedAt);
        Assert.Equal(id, customer.Id);
        Assert.Equal("Nour", customer.Name);
    }

    [Fact]
    public void Delete_Twice_KeepsTheFirstDeletionTime()
    {
        var customer = Customer.Create("Nour", null, null, Created);
        customer.Delete(Later);

        customer.Delete(Later.AddDays(1));

        Assert.Equal(Later, customer.DeletedAt);
    }

    [Fact]
    public void Update_OfADeletedCustomer_Throws()
    {
        var customer = Customer.Create("Nour", null, null, Created);
        customer.Delete(Later);

        Assert.Throws<InvalidOperationException>(() => customer.Update("Other", null, null, Later));
    }
}
