namespace Crm.Application.Auth;

/// <summary>
/// Every permission of the CRM (the whole Phase 1 catalogue, defined in CRM-7). An endpoint requires one with
/// <c>RequireAuthorization(Permissions.X)</c> (the policy name is the permission); roles get them in
/// <see cref="RolePermissions"/>. The values are sent to the client (GET /api/auth/me) and mirrored in
/// client/src/auth/permissions.ts: never rename a value, only add new ones.
/// </summary>
public static class Permissions
{
    /// <summary>List, create, edit, deactivate and reactivate staff users (/api/users).</summary>
    public const string UsersManage = "users.manage";

    /// <summary>Give the SuperAdmin role, or change / deactivate a SuperAdmin (on top of <see cref="UsersManage"/>).</summary>
    public const string UsersManageSuperAdmins = "users.manage-super-admins";

    /// <summary>See customers, their contacts, interaction history, notes and attachments (CRM-8..11).</summary>
    public const string CustomersView = "customers.view";

    /// <summary>Create, edit and (soft) delete customers; add contacts, notes and attachments (CRM-8..11).</summary>
    public const string CustomersManage = "customers.manage";

    /// <summary>See tickets: list, details, thread, history, SLA timers (CRM-14, 15, 18, 20).</summary>
    public const string TicketsView = "tickets.view";

    /// <summary>Create tickets, reply, add internal notes, change status / priority / category (CRM-13, 15, 17).</summary>
    public const string TicketsManage = "tickets.manage";

    /// <summary>Assign a ticket to any agent (CRM-16). Without it a user may not assign tickets to someone else.</summary>
    public const string TicketsAssign = "tickets.assign";

    /// <summary>Read and mark own in-app notifications and connect to the notification hub (CRM-28); every role has it.</summary>
    public const string NotificationsView = "notifications.view";

    /// <summary>Create, edit and deactivate ticket categories (CRM-12 admin settings).</summary>
    public const string CategoriesManage = "categories.manage";

    /// <summary>Change the SLA policy per priority (CRM-19: SuperAdmin only).</summary>
    public const string SlaManage = "sla.manage";

    /// <summary>Configure the Email / WhatsApp channel settings (CRM-23..26 admin settings).</summary>
    public const string ChannelsManage = "channels.manage";

    /// <summary>Open the reports area (sidebar "Reports").</summary>
    public const string ReportsView = "reports.view";

    /// <summary>Every permission, in catalogue order (the order used in /api/auth/me).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        UsersManage, UsersManageSuperAdmins,
        CustomersView, CustomersManage,
        TicketsView, TicketsManage, TicketsAssign, NotificationsView,
        CategoriesManage, SlaManage, ChannelsManage,
        ReportsView,
    ];
}
