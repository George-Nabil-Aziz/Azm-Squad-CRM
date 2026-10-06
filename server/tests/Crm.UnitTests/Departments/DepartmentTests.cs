using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Departments;

public class DepartmentTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsTheName_AndStartsActive()
    {
        var department = Department.Create("  Billing  ", Now);

        Assert.Equal("Billing", department.Name);
        Assert.Equal("BILLING", department.NormalizedName);
        Assert.True(department.IsActive);
        Assert.Equal(Now, department.CreatedAt);
        Assert.NotEqual(Guid.Empty, department.Id);
    }

    [Fact]
    public void Update_RenamesAndDeactivates()
    {
        var department = Department.Create("Billing", Now);

        department.Update("Finance", false, Now.AddHours(1));

        Assert.Equal("Finance", department.Name);
        Assert.False(department.IsActive);
        Assert.Equal(Now.AddHours(1), department.UpdatedAt);
    }

    [Fact]
    public void Create_WithABlankName_Throws() =>
        Assert.ThrowsAny<ArgumentException>(() => Department.Create("  ", Now));

    [Fact]
    public void Create_WithANonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => Department.Create("Billing", DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));

    [Fact]
    public void DepartmentSlaPolicy_ConvertsToAPolicyWithItsMinutes()
    {
        var departmentId = Guid.NewGuid();
        var overridePolicy = DepartmentSlaPolicy.Create(departmentId, TicketPriority.High, 30, 120, Now);

        var policy = overridePolicy.ToPolicy();

        Assert.Equal(departmentId, overridePolicy.DepartmentId);
        Assert.Equal(TicketPriority.High, policy.Priority);
        Assert.Equal(30, policy.ResponseMinutes);
        Assert.Equal(120, policy.ResolutionMinutes);
        Assert.Equal(Now.AddMinutes(30), policy.ResponseDueAt(Now));
    }

    [Fact]
    public void DepartmentSlaPolicy_Update_ChangesTheMinutes()
    {
        var overridePolicy = DepartmentSlaPolicy.Create(Guid.NewGuid(), TicketPriority.Mid, 30, 120, Now);

        overridePolicy.Update(45, 200, Now.AddMinutes(5));

        Assert.Equal(45, overridePolicy.ResponseMinutes);
        Assert.Equal(200, overridePolicy.ResolutionMinutes);
        Assert.Equal(Now.AddMinutes(5), overridePolicy.UpdatedAt);
    }

    [Fact]
    public void DepartmentSlaPolicy_WithResolutionBelowResponse_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DepartmentSlaPolicy.Create(Guid.NewGuid(), TicketPriority.Low, 100, 50, Now));

    [Fact]
    public void Ticket_ChangeDepartment_ReturnsTrueOnlyWhenItChanges()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Subject", null, null, TicketPriority.Mid, TicketChannel.Manual, null, Now);
        var department = Guid.NewGuid();

        Assert.True(ticket.ChangeDepartment(department, Now.AddMinutes(1)));
        Assert.Equal(department, ticket.DepartmentId);
        Assert.Equal(Now.AddMinutes(1), ticket.UpdatedAt);
        Assert.False(ticket.ChangeDepartment(department, Now.AddMinutes(2)));
        Assert.True(ticket.ChangeDepartment(null, Now.AddMinutes(3)));
        Assert.Null(ticket.DepartmentId);
    }
}
