namespace Crm.Domain.Departments;

/// <summary>A staff user is a member of a department (CRM-61). Users live in Infrastructure (Identity), so only the id is kept here.</summary>
public sealed class UserDepartment
{
    private UserDepartment()
    {
        // EF Core materializes memberships through this constructor.
    }

    public UserDepartment(Guid userId, Guid departmentId)
    {
        UserId = userId;
        DepartmentId = departmentId;
    }

    public Guid UserId { get; private set; }

    public Guid DepartmentId { get; private set; }
}
