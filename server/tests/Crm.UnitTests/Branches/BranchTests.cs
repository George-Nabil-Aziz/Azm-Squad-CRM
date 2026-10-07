using Crm.Domain.Branches;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Branches;

public class BranchTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsTheName_AndStartsActive()
    {
        var branch = Branch.Create("  Riyadh  ", Now);

        Assert.Equal("Riyadh", branch.Name);
        Assert.Equal("RIYADH", branch.NormalizedName);
        Assert.True(branch.IsActive);
        Assert.Equal(Now, branch.CreatedAt);
        Assert.NotEqual(Guid.Empty, branch.Id);
    }

    [Fact]
    public void Update_RenamesAndDeactivates()
    {
        var branch = Branch.Create("Riyadh", Now);

        branch.Update("Jeddah", false, Now.AddHours(1));

        Assert.Equal(("Jeddah", false, Now.AddHours(1)), (branch.Name, branch.IsActive, branch.UpdatedAt));
    }

    [Fact]
    public void Create_WithABlankNameOrANonUtcTime_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Branch.Create(" ", Now));
        Assert.Throws<ArgumentException>(() => Branch.Create("Riyadh", DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Customer_ChangeBranch_ReturnsTrueOnlyWhenItChanges()
    {
        var customer = Customer.Create("Nour", null, null, Now);
        var branch = Guid.NewGuid();

        Assert.True(customer.ChangeBranch(branch, Now.AddMinutes(1)));
        Assert.Equal(branch, customer.BranchId);
        Assert.Equal(Now.AddMinutes(1), customer.UpdatedAt);
        Assert.False(customer.ChangeBranch(branch, Now.AddMinutes(2)));
        Assert.True(customer.ChangeBranch(null, Now.AddMinutes(3)));
        Assert.Null(customer.BranchId);
    }

    [Fact]
    public void Ticket_AssignBranch_SetsTheBranch()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Subject", null, null, TicketPriority.Mid, TicketChannel.Manual, null, Now);
        var branch = Guid.NewGuid();

        ticket.AssignBranch(branch);

        Assert.Equal(branch, ticket.BranchId);
    }
}
