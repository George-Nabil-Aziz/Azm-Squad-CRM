using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

/// <summary>Staff user (ASP.NET Identity). Lives in Infrastructure, never in Domain (CLAUDE.md).</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>False after an admin deactivated the user: login is refused and existing tokens stop working.</summary>
    public bool IsActive { get; set; } = true;
}
