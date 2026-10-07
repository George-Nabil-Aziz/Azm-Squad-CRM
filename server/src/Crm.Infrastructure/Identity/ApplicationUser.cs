using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

/// <summary>Staff user (ASP.NET Identity). Lives in Infrastructure, never in Domain (CLAUDE.md).</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>False after an admin deactivated the user: login is refused and existing tokens stop working.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>False while an agent is off duty: automatic assignment (CRM-27) skips them. Does not affect login.</summary>
    public bool IsOnDuty { get; set; } = true;

    /// <summary>The branch the user works in (CRM-62); null = no branch (head office: sees every branch).</summary>
    public Guid? BranchId { get; set; }
}
