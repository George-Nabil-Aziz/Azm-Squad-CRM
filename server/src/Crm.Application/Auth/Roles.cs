namespace Crm.Application.Auth;

/// <summary>Role names seeded at startup (CLAUDE.md "Seed"). CRM-7 builds permissions on top of them.</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Agent = "Agent";

    public static IReadOnlyList<string> All { get; } = [SuperAdmin, Admin, Supervisor, Agent];
}
