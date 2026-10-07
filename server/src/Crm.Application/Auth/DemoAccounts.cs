namespace Crm.Application.Auth;

/// <summary>A development demo sign-in (never carries a password).</summary>
public sealed record DemoAccount(string Email, string Role);

/// <summary>
/// The demo accounts that exist in Development only (they share the password of <c>Seed:SuperAdminPassword</c>).
/// The test host can switch them on with <c>Seed:DemoAccounts=true</c>; no other environment can.
/// </summary>
public static class DemoAccounts
{
    public const string SuperAdminEmail = "superadmin@crm.com";
    public const string AdminEmail = "admin@crm.com";
    public const string LegacySuperAdminEmail = "admin@crm.local";
    public const string SupervisorEmail = "supervisor@crm.com";
    public const string AgentEmail = "agent@crm.com";
    public const string CustomerEmail = "customer@crm.com";
    public const string CustomerRole = "Customer";

    /// <summary>The seeded SuperAdmin first, then the demo staff accounts, then the demo portal customer.</summary>
    public static IReadOnlyList<DemoAccount> All { get; } =
    [
        new(SuperAdminEmail, Roles.SuperAdmin),
        new(AdminEmail, Roles.Admin),
        new(SupervisorEmail, Roles.Supervisor),
        new(AgentEmail, Roles.Agent),
        new(CustomerEmail, CustomerRole),
    ];

    public static string FullNameOf(string role) => $"Demo {role}";

    /// <summary>True in Development, and in Testing when the flag is set. Never in Production or any other environment.</summary>
    public static bool IsEnabled(string? environmentName, string? flag) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase));
}
