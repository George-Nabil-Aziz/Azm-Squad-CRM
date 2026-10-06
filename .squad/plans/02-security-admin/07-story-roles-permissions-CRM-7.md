# Story 07 — Roles & permissions (Story: CRM-7)

## Prerequisites

- Foundation feature completed and merged to `main` ([../01-foundation/00-overview.md](../01-foundation/00-overview.md)):
  - Story 03 [../01-foundation/03-story-authentication-CRM-2.md](../01-foundation/03-story-authentication-CRM-2.md) (CRM-2) — seeded roles (`Roles`, `CrmDbInitializer`), JWT with `role` claims (`RoleClaimType = "role"`), `GET /api/auth/me`, the guard `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous`. Its "6 — How later stories build on this" is **binding**: "CRM-7 builds policies on `Roles` constants / the `role` claim (`RequireAuthorization(policy)`) … Permission claims, if added, go into `JwtAccessTokenGenerator`."
  - Story 04 [../01-foundation/04-story-app-layout-CRM-3.md](../01-foundation/04-story-app-layout-CRM-3.md) (CRM-3) — its section 9 is **binding**: "add an optional `permission` field to `NavigationItem`, filter `navigationItems` in `AppSidebar` with the current user's permissions (from `useCurrentUser()` …), and wrap protected routes in a permission guard next to `RequireAuth` (UI hiding only; the API still enforces authorization)."
  - Story 05 [../01-foundation/05-story-i18n-rtl-CRM-4.md](../01-foundation/05-story-i18n-rtl-CRM-4.md) (CRM-4) — every UI string in `client/src/i18n/{en,ar}.json`; guards `translations.test.ts`, `no-hardcoded-text.test.ts`. **This story adds no UI string and no server text.**
- Story 06 completed: [06-story-user-management-CRM-6.md](06-story-user-management-CRM-6.md) (CRM-6) — `CrmPolicies.ManageUsers`, `ICurrentUser`, `UserService.EnsureMayManage`, `CrmApiFactory.CreateClientWithRoleAsync`, users page. Its section "7 — How later stories build on this" is **binding**: "change only the policy definitions in `AddCrmAuthentication` … Endpoints and `UsersAuthorizationTests` keep working unchanged (Agent/Supervisor → 403, Admin/SuperAdmin → 200)". Merged to `main`.
- Work on branch **`feature/crm-7-roles-permissions`** (already created from `main`).
- Phase 1 order: CRM-1 ✅ → CRM-5 ✅ → CRM-2 ✅ → CRM-3 ✅ → CRM-4 ✅ → CRM-6 ✅ → **CRM-7 (this)** → customers (CRM-8..11) → tickets (CRM-12..18) → SLA (CRM-19..22) → email/WhatsApp (CRM-23..26).
- **No new NuGet or npm packages, no migration, no shadcn component.**
- **Shared contract created here** (every later story uses it): the **permission catalogue** `Crm.Application/Auth/Permissions.cs` + the **role matrix** `Crm.Application/Auth/RolePermissions.cs` (see "Permission catalogue" below), one authorization policy per permission (policy name = permission value), `ICurrentUser.HasPermission`, `permissions` in `GET /api/auth/me`, the guard tests `EveryProtectedApiEndpoint_RequiresAKnownPermission` + `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint`, the test endpoints `/_test/permissions/<permission>`; client `client/src/auth/permissions.ts` (kept equal to the server by `permissions.test.ts`), `usePermissions()`, `<Can>`, `<RequirePermission>`, `NavigationItem.permission`, `fakeApi({ me })` + `superAdminMe` / `agentMe` / `supervisorMe`.

---

## Story Goal

Each staff user can only do what their role allows, enforced by the API and reflected in the UI.

1. The four roles **SuperAdmin, Admin, Supervisor, Agent** stay seeded (AC 1 — already done by CRM-2; covered by the existing `SeedTests.Startup_SeedsRolesAndSuperAdmin`, plus a new unit test that every seeded role has permissions).
2. A **code-defined permission catalogue** for the whole Phase 1 and a **role → permissions matrix**. Every protected `/api/*` endpoint requires a permission policy; a guard test fails when an endpoint has none (AC 4).
3. An **Agent** calling an admin-settings endpoint (`categories.manage`, `sla.manage`, `channels.manage`, `users.manage`) gets **403** ProblemDetails (AC 2). The real settings endpoints come with CRM-12/19/23..26; until then the test endpoints `/_test/permissions/<permission>` prove the policies.
4. **SuperAdmin has every permission**: a test calls every registered `/api/*` endpoint as SuperAdmin and never gets 401/403 (AC 3).
5. `GET /api/auth/me` returns `permissions`; the sidebar shows only the items the user may open, and the routes behind them redirect a user without the permission to the dashboard (AC 5). Agents no longer see **Users** or **Reports**; Supervisors see **Reports** but not **Users**.
6. Users page: an **Admin** no longer sees the "System administrator" (SuperAdmin) role checkbox, nor Edit/Deactivate on SuperAdmin rows (the API already answered 403 — CRM-6).

**Decisions**

- **Permissions are derived from the `role` claims on every request, not stored in the token.** `PermissionAuthorizationHandler` and `/api/auth/me` both call `RolePermissions` with the token's roles. One source of truth (the code), a smaller token, and a matrix change applies to every token immediately after deploy. Role *membership* changes still apply at next login (≤ 60 min, CRM-6 decision); deactivation stays the immediate kill switch. `JwtAccessTokenGenerator` is **not** changed.
- **Policy name = permission value** (`"tickets.assign"`). `AddCrmAuthentication` registers one policy per entry of `Permissions.All` (`RequireAuthenticatedUser()` + `PermissionRequirement`). New endpoints call `.RequireAuthorization(Permissions.X)`. `CrmPolicies.ManageUsers` stays as an alias (`= Permissions.UsersManage`) so the CRM-6 endpoints and tests stay **unchanged** (CRM-6 section 7).
- **SuperAdmin-only user rule becomes a permission** (`users.manage-super-admins`): `UserService.EnsureMayManage` checks `ICurrentUser.HasPermission(...)` instead of `IsInRole(SuperAdmin)` — same behaviour, and the client hides the SuperAdmin option with the same permission.
- **Client route guard redirects to `/`** (dashboard, visible to everyone) — no new page, no new string. While `/api/auth/me` loads, gated items/routes render nothing (no flash of forbidden UI).
- **`/api/auth/me` is the only "signed-in, no permission" endpoint** (allow-list in the guard test). `/api/health` and `/api/auth/login` stay anonymous.

**Not in scope:** editing roles/permissions at runtime (UI or API), custom roles, per-user overrides; the real settings endpoints (CRM-12/19/23..26); pushing role changes into issued tokens; audit of denied attempts; a "no access" page.

---

## Permission catalogue (Phase 1) — binding for every later story

| Constant (`Crm.Application.Auth.Permissions`) | Value | Client key (`permissions.*`) | SuperAdmin | Admin | Supervisor | Agent | Used by |
|---|---|---|---|---|---|---|---|
| `UsersManage` | `users.manage` | `usersManage` | ✓ | ✓ | – | – | `/api/users` (CRM-6), sidebar "Users" |
| `UsersManageSuperAdmins` | `users.manage-super-admins` | `usersManageSuperAdmins` | ✓ | – | – | – | `UserService.EnsureMayManage`, users dialog/table |
| `CustomersView` | `customers.view` | `customersView` | ✓ | ✓ | ✓ | ✓ | CRM-8..11 reads, sidebar "Customers" |
| `CustomersManage` | `customers.manage` | `customersManage` | ✓ | ✓ | ✓ | ✓ | CRM-8 create/edit/soft-delete, CRM-9 contacts, CRM-11 notes/files |
| `TicketsView` | `tickets.view` | `ticketsView` | ✓ | ✓ | ✓ | ✓ | CRM-14 list, CRM-15 details, CRM-18 history, CRM-20 timers, sidebar "Tickets" |
| `TicketsManage` | `tickets.manage` | `ticketsManage` | ✓ | ✓ | ✓ | ✓ | CRM-13 create, CRM-15 replies/notes, CRM-17 status, priority/category changes |
| `TicketsAssign` | `tickets.assign` | `ticketsAssign` | ✓ | ✓ | ✓ | – | CRM-16 assign to someone else |
| `CategoriesManage` | `categories.manage` | `categoriesManage` | ✓ | ✓ | – | – | CRM-12 category admin (reading active categories for the ticket form = `tickets.manage`) |
| `SlaManage` | `sla.manage` | `slaManage` | ✓ | – | – | – | CRM-19 SLA policy (SuperAdmin only) |
| `ChannelsManage` | `channels.manage` | `channelsManage` | ✓ | ✓ | – | – | CRM-23..26 channel settings (webhooks themselves are anonymous + signature-checked) |
| `ReportsView` | `reports.view` | `reportsView` | ✓ | ✓ | ✓ | – | sidebar "Reports" (reports are a later phase) |

Knowledge base and dashboard have **no** permission (visible to every signed-in user).

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules ("Authorization on the API (policies/permissions), never only in the UI", Domain/Application free of ASP.NET), Frontend rules (shadcn only, logical classes, no hard-coded text, API only via `client/src/api`, tests by role/label), **Architecture decisions** (endpoints, `CrmApiFactory`).
2. `.squad/stories/02-security-admin/CRM-7/intake.md` — acceptance criteria 1–5, **Out of scope**.
3. [../01-foundation/03-story-authentication-CRM-2.md](../01-foundation/03-story-authentication-CRM-2.md) lines 1833–1843, [../01-foundation/04-story-app-layout-CRM-3.md](../01-foundation/04-story-app-layout-CRM-3.md) lines 1286–1296 (CRM-7 bullet), [06-story-user-management-CRM-6.md](06-story-user-management-CRM-6.md) lines 1379–1386 (section 7) and line 2413 (Edge case "dialog still shows the SuperAdmin checkbox to Admins … CRM-7 hides it").
4. `server/src/Crm.Application/Auth/Roles.cs` — whole file (12 lines): `Roles.SuperAdmin` … `Roles.All`. `RolePermissions` is keyed by these constants.
5. `server/src/Crm.Api/Auth/AuthenticationExtensions.cs` — whole file (76 lines). Lines 1–6 usings (add `Microsoft.AspNetCore.Authorization`), lines 50–53 the **only** policy definition (`AddAuthorizationBuilder().AddPolicy(CrmPolicies.ManageUsers, policy => policy.RequireRole(...))` — replaced), line 44 `OnTokenValidated = RejectInactiveUserAsync` (unchanged).
6. `server/src/Crm.Api/Auth/CrmPolicies.cs` — whole file (11 lines); line 10 `ManageUsers = "ManageUsers"` becomes an alias of `Permissions.UsersManage`.
7. `server/src/Crm.Api/Auth/JwtAccessTokenGenerator.cs` lines 42–49 `AuthClaimTypes.Role = "role"` (read by the handler). Lines 28–35 token claims — **no change** (permissions are not put in the token).
8. `server/src/Crm.Api/Auth/HttpCurrentUser.cs` — whole file (16 lines); add `HasPermission` after line 15. `server/src/Crm.Application/Common/Security/ICurrentUser.cs` — whole file (10 lines); add the member after line 9.
9. `server/src/Crm.Api/Endpoints/AuthEndpoints.cs` — lines 18–24 `/me` (builds `CurrentUserResponse` from claims). `server/src/Crm.Application/Auth/AuthContracts.cs` line 7 `CurrentUserResponse` (gets `Permissions`).
10. `server/src/Crm.Api/Endpoints/UsersEndpoints.cs` line 11 `.RequireAuthorization(CrmPolicies.ManageUsers)` — **no change** (the alias keeps it).
11. `server/src/Crm.Infrastructure/Identity/UserService.cs` lines 167–174 `EnsureMayManage` (`currentUser.IsInRole(Roles.SuperAdmin)` on line 170 → `HasPermission`). Line 1 already has `using Crm.Application.Auth;`.
12. `server/tests/Crm.Api.IntegrationTests/Auth/ProtectedEndpointTests.cs` — lines 30–43 `ProtectedEndpoint_WithValidToken_Returns200WithCurrentUser` (new `/me` tests go after it), lines 76–91 `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous` (the pattern for the new guard; it stays), line 93 `MeBody` record.
13. `server/tests/Crm.Api.IntegrationTests/Infrastructure/TestEndpointsStartupFilter.cs` — whole file (46 lines): lines 27–45 a **second** `UseRouting` / `UseEndpoints` after the app pipeline (verified: endpoints with authorization metadata there need `UseAuthentication()` + `UseAuthorization()` between them, otherwise 11 of 13 new tests fail).
14. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` lines 94–100 `CreateClientWithRoleAsync(role)`, lines 69–76 `LoginAsync()` (SuperAdmin).
15. `server/tests/Crm.Api.IntegrationTests/Users/UsersAuthorizationTests.cs` — whole file (79 lines): must stay green **unchanged** (AC 4 for `/api/users`). `server/tests/Crm.Api.IntegrationTests/Auth/SeedTests.cs` lines 12–24 (AC 1, unchanged). `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` (Application stays free of ASP.NET — the new Application files use only BCL types).
16. `client/src/api/auth.ts` — lines 15–20 `CurrentUser` (add `permissions`), line 1 imports.
17. `client/src/features/auth/useCurrentUser.ts` — whole file (12 lines): query key `['auth', 'me']`, shared by header, dashboard and the new `usePermissions` (one request).
18. `client/src/app/navigation.ts` — lines 15–21 `NavigationItem`, lines 24–31 `navigationItems`. `client/src/components/layout/AppSidebar.tsx` lines 35–53 (`navigationItems.map` on line 50). `client/src/app/AppRoutes.tsx` — whole file (27 lines). `client/src/app/RequireAuth.tsx` (16 lines) — style of the new route guard.
19. `client/src/features/users/UserFormDialog.tsx` lines 31–35 (hooks), line 126 `roleNames.map`. `client/src/features/users/UsersTable.tsx` lines 13–16, 40–47 (row buttons).
20. `client/src/test/fake-api.ts` — whole file (56 lines): line 12 `me`, lines 14–17 `FakeApiOptions`, line 23 `fakeApi(...)`, lines 36–42 `/api/auth/me` and `/api/users`.
21. `client/src/App.layout.test.tsx` lines 42–44, 127, 142 (read the links / click a link right after the page renders — gated links now appear only after `/api/auth/me` answers).
22. `client/src/pages/users/UsersPage.test.tsx` lines 1–25 (imports, `vi.mock('@/api/users')`), line 58 `beforeEach`, line 215 end. It does **not** mock `@/api/auth` today; the dialog/table now call `useCurrentUser()`, so it must.
23. `client/src/pages/dashboard/DashboardPage.test.tsx` lines 23–28 — `getCurrentUser` mock without `permissions` → `tsc -b` error TS2345 once `CurrentUser.permissions` exists (verified); add `permissions: []`.
24. `client/src/theme.test.ts` lines 1–7 — `/// <reference types="node" />` + `readFileSync(join(import.meta.dirname, …))`: same technique for the server-catalogue test.
25. `.claude/skills/vercel-react-best-practices/SKILL.md` — direct imports, no barrel files.

Verified while planning (fresh scratch clone of `main` in the session scratchpad; every file below was written there and every command run): **`dotnet build` 0 warnings / 0 errors; `dotnet test` 170 passed (76 unit, 94 integration); `npm test` 375 passed in 21 files; `npm run build` OK (Vite chunk-size warning only); `npm run lint` exit 0.** Further findings:

- Red states observed exactly as written below (unit: `CS0103 'Permissions' does not exist`; integration: 14 failed / 80 passed; client: missing module, then 5 failed + `Can.test.tsx` unresolved import).
- `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` **already passes** before the change (the SuperAdmin is in the old `ManageUsers` role list); it is a guard for AC 3 and for every later endpoint.
- Without `UseAuthentication()` / `UseAuthorization()` in `TestEndpointsStartupFilter`, the `/_test/permissions/*` endpoints fail (`Failed: 11, Passed: 2`) — the second routing step has no authorization middleware.
- A policy whose requirement fails for an **anonymous** request answers **401** (challenge), for an authenticated user **403** (`PermissionEndpoint_WithoutToken_Returns401`).
- C# static field initializers run in textual order: `AgentPermissions` must be declared **before** `ByRole` in `RolePermissions`.
- Bash heredocs collapsed `\\.` in a C# regex string → use a verbatim string `@"…\.…"` (as below).
- HTTP smoke on a throw-away LocalDB database (`Crm7SmokeThrowaway`, dropped afterwards): `/me` as SuperAdmin → all 11 permissions; as Agent → `["customers.view","customers.manage","tickets.view","tickets.manage"]`; Agent `GET /api/users` with `Accept-Language: ar` → 403 Arabic title; Admin `GET /api/users` → 200; Admin creating a SuperAdmin → 403 "Only a super administrator can assign …".
- Working-tree files are CRLF (`git ls-files --eol`: `i/lf w/crlf`): edit with the editor tools, not `sed` multi-line replacements.

---

## Backend Tasks

All commands run from `server/`. **No new NuGet packages, no migration.**

### 1 — Unit tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Auth/RolePermissionsTests.cs`**

```csharp
using System.Reflection;
using Crm.Application.Auth;

namespace Crm.UnitTests.Auth;

public class RolePermissionsTests
{
    [Fact]
    public void Catalogue_ListsEveryPermissionConstantOnce()
    {
        var constants = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(constants.Order(StringComparer.Ordinal), Permissions.All.Order(StringComparer.Ordinal));
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Permissions.All, permission => Assert.Matches(@"^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*$", permission));
    }

    [Fact]
    public void EverySeededRole_HasPermissions()
    {
        Assert.All(Roles.All, role => Assert.NotEmpty(RolePermissions.ForRole(role)));
    }

    [Fact]
    public void SuperAdmin_HasEveryPermission()
    {
        Assert.Equal(Permissions.All, RolePermissions.ForRole(Roles.SuperAdmin));
    }

    [Fact]
    public void Admin_HasEverythingExceptTheSuperAdminOnlyPermissions()
    {
        Assert.Equal(
            Permissions.All.Except([Permissions.UsersManageSuperAdmins, Permissions.SlaManage]),
            RolePermissions.ForRole(Roles.Admin));
    }

    [Fact]
    public void Supervisor_WorksTicketsAndCustomers_AssignsTickets_AndSeesReports()
    {
        Assert.Equal(
            [Permissions.CustomersView, Permissions.CustomersManage, Permissions.TicketsView, Permissions.TicketsManage,
             Permissions.TicketsAssign, Permissions.ReportsView],
            RolePermissions.ForRole(Roles.Supervisor));
    }

    [Fact]
    public void Agent_WorksTicketsAndCustomersOnly()
    {
        Assert.Equal(
            [Permissions.CustomersView, Permissions.CustomersManage, Permissions.TicketsView, Permissions.TicketsManage],
            RolePermissions.ForRole(Roles.Agent));
    }

    [Theory]
    [InlineData(Permissions.UsersManage)]
    [InlineData(Permissions.CategoriesManage)]
    [InlineData(Permissions.SlaManage)]
    [InlineData(Permissions.ChannelsManage)]
    public void Agent_HasNoAdminSettingsPermission(string permission)
    {
        Assert.False(RolePermissions.HasPermission([Roles.Agent], permission));
    }

    [Theory]
    [InlineData("admin")] // role names are case-sensitive
    [InlineData("Customer")]
    [InlineData("")]
    public void UnknownRole_HasNoPermissions(string role)
    {
        Assert.Empty(RolePermissions.ForRole(role));
        Assert.False(RolePermissions.HasPermission([role], Permissions.TicketsView));
    }

    [Fact]
    public void ForRoles_CombinesRoles_WithoutDuplicates_InCatalogueOrder()
    {
        var combined = RolePermissions.ForRoles([Roles.Supervisor, Roles.Agent, Roles.Supervisor]);

        Assert.Equal(RolePermissions.ForRole(Roles.Supervisor), combined);
        Assert.Empty(RolePermissions.ForRoles([]));
    }
}
```

Run `dotnet test` → **Red**: compile errors `CS0103: The name 'Permissions' does not exist in the current context` (and `RolePermissions`).

### 2 — Application: catalogue + matrix (Green for unit tests)

**Create file: `server/src/Crm.Application/Auth/Permissions.cs`**

```csharp
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
        TicketsView, TicketsManage, TicketsAssign,
        CategoriesManage, SlaManage, ChannelsManage,
        ReportsView,
    ];
}
```

**Create file: `server/src/Crm.Application/Auth/RolePermissions.cs`**

```csharp
namespace Crm.Application.Auth;

/// <summary>
/// Which permissions each seeded role has (code-defined; there is no permission editing). A user's permissions
/// are the union over their roles. SuperAdmin has every permission, so it can call every endpoint.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyList<string> AgentPermissions =
    [
        Permissions.CustomersView, Permissions.CustomersManage,
        Permissions.TicketsView, Permissions.TicketsManage,
    ];

    private static readonly Dictionary<string, IReadOnlyList<string>> ByRole = new(StringComparer.Ordinal)
    {
        [Roles.SuperAdmin] = Permissions.All,
        [Roles.Admin] = [.. Permissions.All.Except([Permissions.UsersManageSuperAdmins, Permissions.SlaManage])],
        [Roles.Supervisor] = InCatalogueOrder([.. AgentPermissions, Permissions.TicketsAssign, Permissions.ReportsView]),
        [Roles.Agent] = AgentPermissions,
    };

    /// <summary>The permissions of one role (case-sensitive name); an unknown role has none.</summary>
    public static IReadOnlyList<string> ForRole(string role) =>
        ByRole.TryGetValue(role, out var permissions) ? permissions : [];

    /// <summary>The permissions of a user with these roles, without duplicates, in catalogue order.</summary>
    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        InCatalogueOrder(roles.SelectMany(ForRole));

    public static bool HasPermission(IEnumerable<string> roles, string permission) =>
        roles.Any(role => ForRole(role).Contains(permission, StringComparer.Ordinal));

    private static IReadOnlyList<string> InCatalogueOrder(IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        return [.. Permissions.All.Where(set.Contains)];
    }
}
```

**Keep `AgentPermissions` above `ByRole`** (static initializers run top to bottom).

Run `dotnet test` → unit tests **Green: 76 passed** (62 existing + 14 new); integration **79 passed** (unchanged).

### 3 — Integration tests (Red)

**File: `server/tests/Crm.Api.IntegrationTests/Infrastructure/TestEndpointsStartupFilter.cs`** — replace the whole file with:

```csharp
using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Crm.Api.IntegrationTests.Infrastructure;

public sealed record SampleRequest(string? Name, string? Email, int Age);

public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// Test-only endpoints: /_test/errors/* trigger each kind of failure; /_test/permissions/&lt;permission&gt;
/// answers 200 only to users with that permission (one endpoint per permission of the catalogue).
/// </summary>
public sealed class TestEndpointsStartupFilter : IStartupFilter
{
    public const string SecretMessage = "secret-internal-detail-1234";

    public static string PermissionPath(string permission) => $"/_test/permissions/{permission}";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        // This second routing step runs after the app's own pipeline: endpoints with authorization metadata
        // need the authentication + authorization middleware between UseRouting and UseEndpoints.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            var group = endpoints.MapGroup("/_test/errors");
            group.MapPost("/validate", async (SampleRequest request, IValidator<SampleRequest> validator,
                CancellationToken cancellationToken) =>
            {
                await validator.ValidateOrThrowAsync(request, cancellationToken);
                return Results.NoContent();
            });
            group.MapGet("/unhandled", IResult () => throw new InvalidOperationException(SecretMessage));
            group.MapGet("/not-found", IResult () => throw new NotFoundException("Customer 42 was not found."));
            group.MapGet("/conflict", IResult () => throw new ConflictException("Email already in use."));
            group.MapGet("/forbidden", IResult () => throw new ForbiddenException("Agents cannot delete customers."));

            foreach (var permission in Permissions.All)
            {
                endpoints.MapGet(PermissionPath(permission), () => Results.Ok()).RequireAuthorization(permission);
            }
        });
    };
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Auth/PermissionPolicyTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Auth;

public partial class PermissionPolicyTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Endpoints any signed-in user may call (no permission needed). Keep this list short.</summary>
    private static readonly string[] SignedInOnlyEndpoints = ["/api/auth/me"];

    private List<RouteEndpoint> ProtectedApiEndpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true
                        && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();

    [Fact]
    public void EveryProtectedApiEndpoint_RequiresAKnownPermission()
    {
        var endpoints = ProtectedApiEndpoints();

        Assert.NotEmpty(endpoints);
        var withoutPermission = endpoints
            .Where(e => !SignedInOnlyEndpoints.Contains(e.RoutePattern.RawText))
            .Where(e => !e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(data => data.Policy is not null && Permissions.All.Contains(data.Policy)))
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        Assert.Empty(withoutPermission);
    }

    [Fact]
    public async Task SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        var requests = ProtectedApiEndpoints()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (method, path: ConcretePath(e.RoutePattern.RawText!))))
            .ToList();

        Assert.NotEmpty(requests);
        foreach (var (method, path) in requests)
        {
            // Empty JSON body: validation may answer 400 and unknown ids 404, but never 401/403.
            var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
            };
            var response = await superAdmin.SendAsync(request);

            Assert.True(
                response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized),
                $"{method} {path} answered {(int)response.StatusCode} to a SuperAdmin.");
        }
    }

    [Theory]
    [InlineData(Permissions.CategoriesManage)]
    [InlineData(Permissions.SlaManage)]
    [InlineData(Permissions.ChannelsManage)]
    [InlineData(Permissions.UsersManage)]
    public async Task AdminSettingsEndpoint_AsAgent_Returns403(string permission)
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync(TestEndpointsStartupFilter.PermissionPath(permission));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.SuperAdmin, HttpStatusCode.OK)]
    [InlineData(Roles.Admin, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Supervisor, HttpStatusCode.Forbidden)]
    public async Task SlaSettings_OnlySuperAdmin(string role, HttpStatusCode expected)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var response = await client.GetAsync(TestEndpointsStartupFilter.PermissionPath(Permissions.SlaManage));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Supervisor, HttpStatusCode.OK)]
    [InlineData(Roles.Admin, HttpStatusCode.OK)]
    [InlineData(Roles.Agent, HttpStatusCode.Forbidden)]
    public async Task AssignTickets_SupervisorAndAboveOnly(string role, HttpStatusCode expected)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var response = await client.GetAsync(TestEndpointsStartupFilter.PermissionPath(Permissions.TicketsAssign));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task PermissionEndpoint_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(TestEndpointsStartupFilter.PermissionPath(Permissions.TicketsView));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>"/api/users/{id:guid}" → "/api/users/&lt;new guid&gt;"; other parameters become "1".</summary>
    private static string ConcretePath(string pattern) =>
        RouteParameter().Replace(pattern, match =>
            match.Groups["constraint"].Value.Contains("guid", StringComparison.Ordinal) ? Guid.NewGuid().ToString() : "1");

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(?<constraint>[^}]*)\}")]
    private static partial Regex RouteParameter();
}
```

**File: `server/tests/Crm.Api.IntegrationTests/Auth/ProtectedEndpointTests.cs`**

- After line 5 (`using Crm.Api.IntegrationTests.Infrastructure;`) add `using Crm.Application.Auth;`.
- After the closing brace of `ProtectedEndpoint_WithValidToken_Returns200WithCurrentUser` (line 43) add:

```csharp

    [Fact]
    public async Task Me_AsSuperAdmin_ReturnsEveryPermission()
    {
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var me = await client.GetFromJsonAsync<MeBody>(MePath);

        Assert.Equal(Permissions.All, me!.Permissions);
    }

    [Fact]
    public async Task Me_AsAgent_ReturnsOnlyTheAgentPermissions()
    {
        var client = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var me = await client.GetFromJsonAsync<MeBody>(MePath);

        Assert.Equal(["customers.view", "customers.manage", "tickets.view", "tickets.manage"], me!.Permissions);
    }
```

- Line 93: `private sealed record MeBody(Guid Id, string Email, string FullName, string[] Roles);` → `private sealed record MeBody(Guid Id, string Email, string FullName, string[] Roles, string[] Permissions);` (non-nullable array; a nullable one gives warning CS8604 in `Assert.Equal`).

Run `dotnet test` → **Red**: unit 76 passed; integration **Failed: 14, Passed: 80** (94 total): all of `AdminSettingsEndpoint_AsAgent_Returns403` ×4, `SlaSettings_OnlySuperAdmin` ×3, `AssignTickets_SupervisorAndAboveOnly` ×3, `PermissionEndpoint_WithoutToken_Returns401` (no policy named like the permission yet → 500), `EveryProtectedApiEndpoint_RequiresAKnownPermission` (`/api/users…` use policy `"ManageUsers"`, not a catalogue value), `Me_AsSuperAdmin_…`, `Me_AsAgent_…` (no `permissions` in `/me`). `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` already passes (guard; see "Verified while planning").

### 4 — Api + Infrastructure (Green)

**Create file: `server/src/Crm.Api/Auth/PermissionAuthorizationHandler.cs`**

```csharp
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Auth;

/// <summary>Requirement of the policy named after <see cref="Permission"/> (one policy per permission).</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Succeeds when one of the user's <c>role</c> claims grants the permission (<see cref="RolePermissions"/>).
/// Permissions are not stored in the token: they are derived from the roles on every request, so the code-defined
/// matrix is the single source of truth.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value);
        if (RolePermissions.HasPermission(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

**File: `server/src/Crm.Api/Auth/AuthenticationExtensions.cs`**

- After line 4 (`using Microsoft.AspNetCore.Authentication.JwtBearer;`) add `using Microsoft.AspNetCore.Authorization;`.
- Replace lines 50–53 (the comment "Endpoints use policy names only. CRM-7 replaces RequireRole …", `services.AddAuthorizationBuilder()`, `.AddPolicy(CrmPolicies.ManageUsers, …RequireRole…)`, `return services;`) with:

```csharp
        // One policy per permission, named like the permission (Permissions.All). Endpoints name the permission they
        // need; PermissionAuthorizationHandler checks it against the user's role claims (RolePermissions).
        var authorization = services.AddAuthorizationBuilder();
        foreach (var permission in Permissions.All)
        {
            authorization.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
```

`using Crm.Application.Auth;` (line 2) is already there. Nothing else in the file changes.

**File: `server/src/Crm.Api/Auth/CrmPolicies.cs`** — replace the whole file:

```csharp
using Crm.Application.Auth;

namespace Crm.Api.Auth;

/// <summary>
/// Authorization policy names. Since CRM-7 every permission in <see cref="Permissions.All"/> is a policy with the
/// same name (registered in <see cref="AuthenticationExtensions.AddCrmAuthentication"/>), so new endpoints call
/// <c>RequireAuthorization(Permissions.X)</c> directly. The alias below keeps the CRM-6 endpoints unchanged.
/// </summary>
public static class CrmPolicies
{
    /// <summary>Create, edit, deactivate and list staff users (/api/users).</summary>
    public const string ManageUsers = Permissions.UsersManage;
}
```

**File: `server/src/Crm.Application/Common/Security/ICurrentUser.cs`** — after line 9 (`bool IsInRole(string role);`) add:

```csharp

    /// <summary>True when one of the user's roles grants the permission (<c>Crm.Application.Auth.Permissions</c>).</summary>
    bool HasPermission(string permission);
```

**File: `server/src/Crm.Api/Auth/HttpCurrentUser.cs`** — add `using Crm.Application.Auth;` after line 2 and, after line 15 (`public bool IsInRole(...)`), add:

```csharp

    public bool HasPermission(string permission) =>
        Principal is not null
        && RolePermissions.HasPermission(Principal.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value), permission);
```

**File: `server/src/Crm.Application/Auth/AuthContracts.cs`** — replace line 7 with:

```csharp
/// <summary>GET /api/auth/me. <c>Permissions</c> = the union of the roles' permissions (<see cref="RolePermissions"/>), in catalogue order.</summary>
public sealed record CurrentUserResponse(
    Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
```

**File: `server/src/Crm.Api/Endpoints/AuthEndpoints.cs`** — replace lines 18–24 (the `/me` mapping) with:

```csharp
        // Any signed-in user (no permission): the client reads its permissions here to hide menu items and actions.
        group.MapGet("/me", (ClaimsPrincipal user) =>
            {
                string[] roles = [.. user.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value)];
                return Results.Ok(new CurrentUserResponse(
                    Guid.Parse(user.FindFirstValue(AuthClaimTypes.UserId)!),
                    user.FindFirstValue(AuthClaimTypes.Email) ?? string.Empty,
                    user.FindFirstValue(AuthClaimTypes.Name) ?? string.Empty,
                    roles,
                    RolePermissions.ForRoles(roles)));
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser");
```

**File: `server/src/Crm.Infrastructure/Identity/UserService.cs`** — replace lines 167–174 (`EnsureMayManage` with its summary) with:

```csharp
    /// <summary>
    /// Only a user with <see cref="Permissions.UsersManageSuperAdmins"/> (SuperAdmin) may give the SuperAdmin role or
    /// change a SuperAdmin (no privilege escalation by an Admin).
    /// </summary>
    private void EnsureMayManage(IEnumerable<string> roles)
    {
        if (roles.Contains(Roles.SuperAdmin) && !currentUser.HasPermission(Permissions.UsersManageSuperAdmins))
        {
            throw new ForbiddenException(UserText.SuperAdminOnly);
        }
    }
```

**Do not change** `UsersEndpoints.cs`, `JwtAccessTokenGenerator.cs`, `UsersAuthorizationTests.cs`, `CrmDbInitializer.cs`.

Run `dotnet build` → **0 warnings, 0 errors**. Run `dotnet test` → **Green: 170 passed** (76 unit, 94 integration).

### 5 — How later stories build on this (write nothing here; for later planners)

- **Every new endpoint (CRM-8 onwards):** `.RequireAuthorization(Permissions.X)` on the group or the endpoint, with X from the catalogue table above (e.g. customers group `RequireAuthorization(Permissions.CustomersView)` plus `RequireAuthorization(Permissions.CustomersManage)` on the write endpoints — several `RequireAuthorization` calls combine with AND). Never `RequireRole`, never a bare `RequireAuthorization()` (only `/api/auth/me` is on the allow-list in `PermissionPolicyTests.SignedInOnlyEndpoints`). `EveryProtectedApiEndpoint_RequiresAKnownPermission` fails otherwise; `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` automatically calls the new endpoint as SuperAdmin.
- **Webhooks (CRM-26 WhatsApp, inbound email if HTTP):** `.AllowAnonymous()` + signature / verify-token check — they are not user calls.
- **A new permission** (only if the table really lacks one): add a `const` + entry in `Permissions.All` (`Crm.Application/Auth/Permissions.cs`), give it to roles in `RolePermissions`, add the same value to `client/src/auth/permissions.ts` (`permissions.test.ts` fails until both lists match), update the unit tests in `RolePermissionsTests` that pin a role's list, and update the catalogue table in the story plan. Never rename an existing value (the client and tests use it).
- **Rules finer than a policy** (CRM-16 "an agent without the assign permission assigning to someone else gets 403", CRM-15 edit-own-reply …): inject `ICurrentUser` and check `currentUser.HasPermission(Permissions.TicketsAssign)` + `currentUser.UserId` in the service; throw `ForbiddenException(<Feature>Text.…)` → 403 ProblemDetails. Self-assignment by an Agent is a `tickets.manage` action decided by CRM-16.
- **Settings stories (CRM-12 categories, CRM-19 SLA, CRM-23..26 channels):** AC "non-X gets 403" → copy `PermissionPolicyTests.AdminSettingsEndpoint_AsAgent_Returns403` against the real route; SLA = `Permissions.SlaManage` (Admin → 403, SuperAdmin → 200, see `SlaSettings_OnlySuperAdmin`).
- **Integration tests per role:** `await factory.CreateClientWithRoleAsync(Roles.Agent)` (also `Roles.Supervisor`, `Roles.Admin`); SuperAdmin = `factory.CreateAuthenticatedClient(await factory.LoginAsync())`. `/_test/permissions/<permission>` exists for every catalogue entry if a test needs "a user with permission X" without a real endpoint.
- **Client — new sidebar area / page:** give the `navigationItems` entry the same `permission` as its route, and wrap the route in `<Route element={<RequirePermission permission={permissions.X} />}>` in `AppRoutes.tsx` (the CRM-8/CRM-12 stories replace the `ComingSoonPage` element *inside* the existing guard). Buttons/actions: `<Can permission={permissions.ticketsAssign}>…</Can>` or `const { can } = usePermissions()`. Hiding is UI only — the API test for the same permission is mandatory.
- **Client tests:** App-level tests use `fakeApi({ me: agentMe })` / `supervisorMe` / default `superAdminMe` from `client/src/test/fake-api.ts`; page tests that render something calling `useCurrentUser()` / `usePermissions()` must `vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))` and resolve a `CurrentUser` **with `permissions`** (see `UsersPage.test.tsx`). Gated sidebar links appear only after `/api/auth/me` answers → use `findByRole`, not `getByRole`.
- **Role changes in tokens:** still applied at next login (≤ 60 min). If a story needs immediate effect, refresh roles in `RejectInactiveUserAsync` (`OnTokenValidated`) — the matrix itself needs no token change.

---

## Frontend Tasks

All commands run from `client/`. **No new npm packages, no new shadcn component, no new UI string** (`en.json` / `ar.json` unchanged). Follow `vercel-react-best-practices` (direct imports, no barrel files).

### 1 — Permission names shared with the server (Red → Green)

**Create file: `client/src/auth/permissions.test.ts`**

```ts
/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { permissions } from './permissions'

// The server's catalogue is the source of truth; this list must name exactly the same permissions.
const serverSource = readFileSync(
  join(import.meta.dirname, '../../../server/src/Crm.Application/Auth/Permissions.cs'),
  'utf8',
)

describe('permissions', () => {
  it('lists exactly the permissions of the server catalogue', () => {
    const serverPermissions = [...serverSource.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1])

    expect(serverPermissions.length).toBeGreaterThan(0)
    expect(Object.values(permissions).toSorted()).toEqual(serverPermissions.toSorted())
  })
})
```

Run `npm test` → **Red**: `Failed to resolve import "./permissions"`.

**Create file: `client/src/auth/permissions.ts`**

```ts
/**
 * Permission names, the same as the server catalogue (Crm.Application.Auth.Permissions; permissions.test.ts keeps
 * both lists equal). GET /api/auth/me returns the signed-in user's permissions; the UI only hides what the user
 * may not use — the API enforces every permission itself.
 */
export const permissions = {
  usersManage: 'users.manage',
  usersManageSuperAdmins: 'users.manage-super-admins',
  customersView: 'customers.view',
  customersManage: 'customers.manage',
  ticketsView: 'tickets.view',
  ticketsManage: 'tickets.manage',
  ticketsAssign: 'tickets.assign',
  categoriesManage: 'categories.manage',
  slaManage: 'sla.manage',
  channelsManage: 'channels.manage',
  reportsView: 'reports.view',
} as const

export type Permission = (typeof permissions)[keyof typeof permissions]
```

Run `npm test` → **Green: 365 passed** (364 + 1).

### 2 — Behaviour tests first (Red)

**File: `client/src/test/fake-api.ts`** — replace the whole file with:

```ts
import { fireEvent, screen } from '@testing-library/react'
import { vi } from 'vitest'
import type { CurrentUser } from '@/api/auth'
import { permissions } from '@/auth/permissions'

export const ADMIN_PASSWORD = 'Admin#12345'

export const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

/** GET /api/auth/me of the seeded SuperAdmin: every permission. */
export const superAdminMe: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: Object.values(permissions),
}

/** Same permissions as the server gives the Agent role (Crm.Application.Auth.RolePermissions). */
export const agentMe: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}

/** Same permissions as the server gives the Supervisor role. */
export const supervisorMe: CurrentUser = {
  id: '3',
  email: 'lead@crm.local',
  fullName: 'Team Lead',
  roles: ['Supervisor'],
  permissions: [...agentMe.permissions, permissions.ticketsAssign, permissions.reportsView],
}

interface FakeApiOptions {
  /** Status returned by GET /api/health (default 200). */
  healthStatus?: number
  /** Body of GET /api/auth/me (default: the SuperAdmin). */
  me?: CurrentUser
}

/**
 * Fake API behind a stubbed fetch: login accepts ADMIN_PASSWORD and returns "good-token";
 * /api/auth/me needs that token and returns `me`; /api/health is ok unless healthStatus says otherwise.
 */
export function fakeApi({ healthStatus = 200, me = superAdminMe }: FakeApiOptions = {}) {
  return vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/health') {
      return healthStatus === 200
        ? json(200, { status: 'ok' })
        : json(healthStatus, { status: healthStatus, correlationId: 'health-1' }, 'application/problem+json')
    }
    if (path === '/api/auth/login') {
      const { password } = JSON.parse(String(init?.body)) as { password: string }
      return password === ADMIN_PASSWORD
        ? json(200, { accessToken: 'good-token', tokenType: 'Bearer', expiresAt: inOneHour() })
        : json(401, { status: 401, title: 'Authentication failed.', correlationId: 'c-401' }, 'application/problem+json')
    }
    if (path === '/api/auth/me') {
      const auth = ((init?.headers ?? {}) as Record<string, string>).Authorization
      return auth === 'Bearer good-token' ? json(200, me) : json(401, { status: 401 }, 'application/problem+json')
    }
    if (path === '/api/users' || path.startsWith('/api/users?')) {
      const { id, email, fullName, roles } = superAdminMe
      return json(200, { items: [{ id, email, fullName, roles, isActive: true }], page: 1, pageSize: 20, totalCount: 1 })
    }
    return json(404, { status: 404 }, 'application/problem+json')
  })
}

/** Number of fetch calls made to `path`. */
export function callsTo(fetchMock: ReturnType<typeof fakeApi>, path: string): number {
  return fetchMock.mock.calls.filter(([calledPath]) => calledPath === path).length
}

export function submitSignIn(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}
```

(`CurrentUser` gets `permissions` in task 3; Vitest does not type-check, `npm run build` does.)

**Create file: `client/src/App.permissions.test.tsx`** (AC 5)

```tsx
import { render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { agentMe, callsTo, fakeApi, inOneHour, supervisorMe } from './test/fake-api'

function renderSignedInAt(path: string) {
  saveSession('good-token', inOneHour())
  window.history.replaceState(null, '', path)
  return render(<App />)
}

async function navigationLinks() {
  const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })
  // Items that need a permission appear once GET /api/auth/me has answered.
  await within(navigation).findByRole('link', { name: 'Tickets' })
  return within(navigation).getAllByRole('link').map((link) => link.textContent)
}

describe('Menu and pages follow the user permissions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hides the menu items an agent has no permission for', async () => {
    vi.stubGlobal('fetch', fakeApi({ me: agentMe }))
    renderSignedInAt('/')

    expect(await navigationLinks()).toEqual(['Dashboard', 'Tickets', 'Customers', 'Knowledge base'])
  })

  it('shows Reports but not Users to a supervisor', async () => {
    vi.stubGlobal('fetch', fakeApi({ me: supervisorMe }))
    renderSignedInAt('/')

    expect(await navigationLinks()).toEqual(['Dashboard', 'Tickets', 'Customers', 'Knowledge base', 'Reports'])
  })

  it('sends a user without permission from /users to the dashboard without calling the users API', async () => {
    const fetchMock = fakeApi({ me: agentMe })
    vi.stubGlobal('fetch', fetchMock)
    renderSignedInAt('/users')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
    expect(fetchMock.mock.calls.some(([path]) => String(path).startsWith('/api/users'))).toBe(false)
    expect(callsTo(fetchMock, '/api/auth/me')).toBe(1)
  })

  it('opens /users for a user with the permission', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderSignedInAt('/users')

    expect(await screen.findByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/users')
  })
})
```

**Create file: `client/src/features/auth/Can.test.tsx`**

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser } from '@/api/auth'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { Can } from './Can'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

function renderCan() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <Can permission={permissions.ticketsView}>
        <p>Ticket list</p>
      </Can>
      <Can permission={permissions.ticketsAssign}>
        <button type="button">Assign</button>
      </Can>
    </QueryClientProvider>,
  )
}

describe('Can', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset()
  })

  it('shows its content when the user has the permission', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({
      id: '1',
      email: 'lead@crm.local',
      fullName: 'Team Lead',
      roles: ['Supervisor'],
      permissions: [permissions.ticketsView, permissions.ticketsAssign],
    })
    renderCan()

    expect(await screen.findByRole('button', { name: 'Assign' })).toBeInTheDocument()
  })

  it('hides its content when the user lacks the permission', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({
      id: '2',
      email: 'agent@crm.local',
      fullName: 'Sara Agent',
      roles: ['Agent'],
      permissions: [permissions.ticketsView],
    })
    renderCan()

    expect(await screen.findByText('Ticket list')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assign' })).not.toBeInTheDocument()
  })

  it('hides its content while the permissions are loading', () => {
    vi.mocked(getCurrentUser).mockReturnValue(new Promise(() => {}))
    renderCan()

    expect(screen.queryByText('Ticket list')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assign' })).not.toBeInTheDocument()
  })
})
```

("Ticket list" / "Assign" are test-only texts inside a test file; the no-hard-coded-text guard skips `*.test.tsx`.)

**File: `client/src/App.layout.test.tsx`** (existing tests: gated links appear after `/api/auth/me`)

- Line 42–43: after `const navigation = screen.getByRole('navigation', { name: 'Main navigation' })` insert

```tsx
    // Items that need a permission appear once GET /api/auth/me has answered (the SuperAdmin sees all of them).
    await within(navigation).findByRole('link', { name: 'Users' })
```

- Line 127: `fireEvent.click(within(navigation).getByRole('link', { name: 'Users' }))` → `fireEvent.click(await within(navigation).findByRole('link', { name: 'Users' }))`.
- Line 142: `fireEvent.click(within(navigation).getByRole('link', { name: label }))` → `fireEvent.click(await within(navigation).findByRole('link', { name: label }))`.

**File: `client/src/pages/users/UsersPage.test.tsx`**

- After line 3 (`import { beforeEach, … } from 'vitest'`) add `import { getCurrentUser, type CurrentUser } from '@/api/auth'`.
- After line 14 (`import { createQueryClient } from '@/app/query-client'`) add `import { permissions } from '@/auth/permissions'`.
- After line 16 (`import { UsersPage } from './UsersPage'`) add:

```tsx

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

/** The signed-in user: a SuperAdmin (every permission) unless a test signs in as an Admin. */
const signedInSuperAdmin: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: Object.values(permissions),
}
const signedInAdmin: CurrentUser = {
  id: '9',
  email: 'office@crm.local',
  fullName: 'Office Admin',
  roles: ['Admin'],
  permissions: Object.values(permissions).filter(
    (permission) => permission !== permissions.usersManageSuperAdmins && permission !== permissions.slaManage,
  ),
}
```

- First line of the `beforeEach` (line 58): add `vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInSuperAdmin)` before `vi.mocked(listUsers)…`.
- Before the final `})` (line 215) add:

```tsx

  it('lets a SuperAdmin manage SuperAdmins: row buttons and the SuperAdmin role', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })
    expect(await within(rowOf('System Administrator')).findByRole('button', { name: 'Edit' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })

    expect(within(dialog).getByRole('checkbox', { name: 'System administrator' })).toBeInTheDocument()
  })

  it('hides the SuperAdmin role from an Admin', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInAdmin)
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })

    expect(within(dialog).getByRole('checkbox', { name: 'Administrator' })).toBeInTheDocument()
    expect(within(dialog).queryByRole('checkbox', { name: 'System administrator' })).not.toBeInTheDocument()
  })

  it('shows an Admin no edit or deactivate button on SuperAdmin rows', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInAdmin)
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    await waitFor(() =>
      expect(within(rowOf('System Administrator')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument(),
    )
    expect(within(rowOf('System Administrator')).queryByRole('button', { name: 'Deactivate' })).not.toBeInTheDocument()
    expect(within(rowOf('Sara Agent')).getByRole('button', { name: 'Edit' })).toBeInTheDocument()
  })
```

**File: `client/src/pages/dashboard/DashboardPage.test.tsx`** — lines 23–28: add `permissions: [],` after `roles: ['SuperAdmin'],` (otherwise `tsc -b` fails with TS2345 once task 3 lands).

Run `npm test` → **Red**: `Test Files 3 failed | 18 passed (21)`, `Tests 5 failed | 367 passed (372)` — `Can.test.tsx` (`Failed to resolve import "./Can"`), `hides the menu items an agent has no permission for` and `shows Reports but not Users to a supervisor` (all 6 links shown), `sends a user without permission from /users …` (users page opens), `hides the SuperAdmin role from an Admin`, `shows an Admin no edit or deactivate button on SuperAdmin rows`.

### 3 — Implementation (Green)

**File: `client/src/api/auth.ts`**

- Line 1: add `import type { Permission } from '@/auth/permissions'` above `import { apiGet, apiPost } from './client'`.
- In `CurrentUser` (lines 15–20) after `roles: string[]` add:

```ts
  /** Permissions of the user's roles (server: RolePermissions), in catalogue order. */
  permissions: Permission[]
```

**Create file: `client/src/features/auth/usePermissions.ts`**

```ts
import type { Permission } from '@/auth/permissions'
import { useCurrentUser } from './useCurrentUser'

/**
 * The signed-in user's permissions (from GET /api/auth/me, shared React Query cache).
 * `can` answers false while they are loading, so nothing protected flashes up before the answer.
 */
export function usePermissions() {
  const { data: user, isPending } = useCurrentUser()

  return {
    isLoading: isPending,
    can: (permission: Permission) => user?.permissions.includes(permission) ?? false,
  }
}
```

**Create file: `client/src/features/auth/Can.tsx`**

```tsx
import type { ReactNode } from 'react'
import type { Permission } from '@/auth/permissions'
import { usePermissions } from './usePermissions'

/** Renders its children only for users with the permission (hides UI only; the API still checks it). */
export function Can({ permission, children }: { permission: Permission; children: ReactNode }) {
  const { can } = usePermissions()

  return can(permission) ? children : null
}
```

**Create file: `client/src/app/RequirePermission.tsx`**

```tsx
import { Navigate, Outlet } from 'react-router'
import type { Permission } from '@/auth/permissions'
import { usePermissions } from '@/features/auth/usePermissions'

/**
 * Renders the child routes only for users with the permission; others go to the dashboard (no page loads,
 * no API call). Renders nothing while the permissions load. Place it inside RequireAuth.
 */
export function RequirePermission({ permission }: { permission: Permission }) {
  const { can, isLoading } = usePermissions()

  if (isLoading) return null
  return can(permission) ? <Outlet /> : <Navigate to="/" replace />
}
```

**File: `client/src/app/navigation.ts`**

- After line 9 (`} from 'lucide-react'`) add `import { permissions, type Permission } from '@/auth/permissions'`.
- In `NavigationItem`, after `icon: LucideIcon` (line 20) add:

```ts
  /** Shown only to users with this permission (its route is wrapped in RequirePermission with the same one). */
  permission?: Permission
```

- Replace the items (lines 25–30) with:

```ts
  { id: 'dashboard', path: '/', icon: LayoutDashboardIcon },
  { id: 'tickets', path: '/tickets', icon: TicketIcon, permission: permissions.ticketsView },
  { id: 'customers', path: '/customers', icon: UsersIcon, permission: permissions.customersView },
  { id: 'knowledgeBase', path: '/knowledge-base', icon: BookOpenIcon },
  { id: 'reports', path: '/reports', icon: ChartColumnIcon, permission: permissions.reportsView },
  { id: 'users', path: '/users', icon: UserCogIcon, permission: permissions.usersManage },
```

**File: `client/src/components/layout/AppSidebar.tsx`**

- After line 14 (`} from '@/components/ui/sidebar'`) add `import { usePermissions } from '@/features/auth/usePermissions'`.
- In `AppSidebar` replace lines 36–37 (`const { t, i18n } = useTranslation()` / `const dir = i18n.dir()`) with:

```tsx
  const { t, i18n } = useTranslation()
  const { can } = usePermissions()
  const dir = i18n.dir()
  // Items with a permission appear once the user's permissions have loaded, and only if the user has it.
  const visibleItems = navigationItems.filter((item) => !item.permission || can(item.permission))
```

- Line 50: `{navigationItems.map((item) => (` → `{visibleItems.map((item) => (`.

**File: `client/src/app/AppRoutes.tsx`** — replace the whole file:

```tsx
import { Navigate, Route, Routes } from 'react-router'
import { permissions } from '@/auth/permissions'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { ComingSoonPage } from '@/pages/coming-soon/ComingSoonPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { UsersPage } from '@/pages/users/UsersPage'
import { RequireAuth } from './RequireAuth'
import { RequirePermission } from './RequirePermission'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          {/* Same permission as the area's item in navigation.ts. */}
          <Route element={<RequirePermission permission={permissions.usersManage} />}>
            <Route path="users" element={<UsersPage />} />
          </Route>
          {/* Areas built by later stories: each story replaces its line with the real page routes. */}
          <Route element={<RequirePermission permission={permissions.ticketsView} />}>
            <Route path="tickets" element={<ComingSoonPage area="tickets" />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.customersView} />}>
            <Route path="customers" element={<ComingSoonPage area="customers" />} />
          </Route>
          <Route path="knowledge-base" element={<ComingSoonPage area="knowledgeBase" />} />
          <Route element={<RequirePermission permission={permissions.reportsView} />}>
            <Route path="reports" element={<ComingSoonPage area="reports" />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
```

**File: `client/src/features/users/UserFormDialog.tsx`**

- After line 8 (`import { createUser, roleNames, updateUser, type User } from '@/api/users'`) add `import { permissions } from '@/auth/permissions'`; after line 20 (`import { Input } from '@/components/ui/input'`) add `import { usePermissions } from '@/features/auth/usePermissions'`.
- After line 33 (`const queryClient = useQueryClient()`) add:

```tsx
  const { can } = usePermissions()
  // Only a SuperAdmin may give the SuperAdmin role (the API refuses it with 403 for everyone else).
  const assignableRoles = roleNames.filter((role) => role !== 'SuperAdmin' || can(permissions.usersManageSuperAdmins))
```

- Line 126: `{roleNames.map((role) => (` → `{assignableRoles.map((role) => (`.

**File: `client/src/features/users/UsersTable.tsx`**

- After line 2 (`import type { User } from '@/api/users'`) add `import { permissions } from '@/auth/permissions'`; after line 5 (`import { Table, … } from '@/components/ui/table'`) add `import { usePermissions } from '@/features/auth/usePermissions'`.
- After line 14 (`const { t, i18n } = useTranslation()`) add:

```tsx
  const { can } = usePermissions()
  // A SuperAdmin account can only be changed by a SuperAdmin (the API answers 403 to everyone else).
  const mayChange = (user: User) => !user.roles.includes('SuperAdmin') || can(permissions.usersManageSuperAdmins)
```

- Replace lines 41–46 (the `<div className="flex justify-end gap-2">` … `</div>` inside the last `TableCell`) with:

```tsx
              {mayChange(user) ? (
                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={() => onEdit(user)}>
                    {t('users.edit')}
                  </Button>
                  <UserStatusAction user={user} />
                </div>
              ) : null}
```

Run `npm test` → **Green: 375 passed in 21 files**. Run `npm run build` → OK (only the existing "chunks larger than 500 kB" warning). Run `npm run lint` → exit 0, no output.

---

## Edge Cases & Failure Modes

- **Endpoint added later without a permission** (bare `RequireAuthorization()` or `RequireRole`) → `EveryProtectedApiEndpoint_RequiresAKnownPermission` lists its route. **Typo in a policy name** (`"ticket.view"`) → same test fails (not in `Permissions.All`); at runtime ASP.NET would throw `InvalidOperationException: The AuthorizationPolicy named … was not found` → 500.
- **Anonymous call to a permission endpoint** → policy has `RequireAuthenticatedUser()` → 401 + `WWW-Authenticate: Bearer` (`PermissionEndpoint_WithoutToken_Returns401`, `UsersApi_WithoutToken_Returns401`).
- **Authenticated user without the permission** → 403, empty body turned into ProblemDetails by `UseStatusCodePages` (title `ErrorText.Forbidden`, Arabic with `Accept-Language: ar` — smoke-verified). Covered by `AdminSettingsEndpoint_AsAgent_Returns403`, `UsersAuthorizationTests`.
- **Unknown / misspelled role claim** (`"admin"`, old token with a removed role) → no permissions (`RolePermissions.ForRole` returns empty; case-sensitive, `StringComparer.Ordinal`) → 403; `/me` returns `permissions: []`. `UnknownRole_HasNoPermissions`.
- **User with several roles** → union, no duplicates, catalogue order (`ForRoles`). `ForRoles_CombinesRoles_WithoutDuplicates_InCatalogueOrder`.
- **Role changed while the user holds a token** → old roles (and so old permissions) until the token expires / next login (≤ 60 min). Deactivation still cuts access at once (`RejectInactiveUserAsync`, unchanged). **Matrix changed in code** → applies to every existing token at the next request (permissions are not in the token).
- **Forged token with `role: SuperAdmin`** → signature check fails first (401, `ProtectedEndpoint_WithTokenSignedByAnotherKey_Returns401`); a valid token of a deleted user → 401 (`OnTokenValidated`).
- **SuperAdmin on an endpoint whose service throws `ForbiddenException`** → would make `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` fail with the route and status in the message. Service-level rules must always let a holder of every permission through (as `EnsureMayManage` does).
- **`SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` sends `{}` to every POST/PUT** → later endpoints must answer 400/404/409 to an empty body, never 500 (an unhandled 500 still passes this test but shows up in logs); endpoints with side effects on an empty body (none today) would run them in the shared test DB.
- **Client: `/api/auth/me` still loading** → `can()` is `false`, `isLoading` true: gated sidebar items are not rendered, `RequirePermission` renders nothing; once loaded they appear (`App.layout.test.tsx` waits with `findByRole`).
- **Client: `/api/auth/me` fails with 500 / network** → toast (ApiErrorToaster), `can()` false: gated items hidden, gated routes redirect to `/`. 401 → session cleared → `/login` (CRM-2 rule, unchanged).
- **Client: user types `/users` without permission** → `<Navigate to="/" replace />`, no `/api/users` call (`sends a user without permission from /users …`). Server would answer 403 anyway.
- **Client and server catalogue drift** (value added on one side only) → `permissions.test.ts` fails (it reads `server/src/Crm.Application/Auth/Permissions.cs` from disk; the path is relative to `client/src/auth/`, so the test needs the full repo checkout — true locally and in a normal CI checkout).
- **Admin edits a user whose roles include SuperAdmin** → no Edit / Deactivate / Reactivate buttons for them; API still answers 403 (`Admin_CannotDeactivateTheSuperAdmin_Returns403`, CRM-6).
- **Admin removes their own `Admin` role** → allowed (CRM-6 decision); they keep access until next login.
- **No new UI text** → `translations.test.ts` and `no-hardcoded-text.test.ts` unchanged and green; comments must not quote English UI strings (CRM-6 finding) — the new comments do not.

---

## Test Plan

1. **Unit (new)** — `server/tests/Crm.UnitTests/Auth/RolePermissionsTests.cs`: `Catalogue_ListsEveryPermissionConstantOnce`, `EverySeededRole_HasPermissions` (AC 1), `SuperAdmin_HasEveryPermission` (AC 3), `Admin_HasEverythingExceptTheSuperAdminOnlyPermissions`, `Supervisor_WorksTicketsAndCustomers_AssignsTickets_AndSeesReports`, `Agent_WorksTicketsAndCustomersOnly`, `Agent_HasNoAdminSettingsPermission` ×4 (AC 2), `UnknownRole_HasNoPermissions` ×3, `ForRoles_CombinesRoles_WithoutDuplicates_InCatalogueOrder` — 14 tests, no database.
2. **Integration (new)** — `server/tests/Crm.Api.IntegrationTests/Auth/PermissionPolicyTests.cs`: `EveryProtectedApiEndpoint_RequiresAKnownPermission` (AC 4, architecture guard), `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` (AC 3), `AdminSettingsEndpoint_AsAgent_Returns403` ×4 (AC 2), `SlaSettings_OnlySuperAdmin` ×3, `AssignTickets_SupervisorAndAboveOnly` ×3, `PermissionEndpoint_WithoutToken_Returns401` — 13 tests.
3. **Integration (modified)** — `ProtectedEndpointTests`: new `Me_AsSuperAdmin_ReturnsEveryPermission`, `Me_AsAgent_ReturnsOnlyTheAgentPermissions`; `MeBody` gets `Permissions`.
4. **Test host (modified)** — `TestEndpointsStartupFilter`: `PermissionPath(permission)`, one `/_test/permissions/<permission>` endpoint per catalogue entry, authentication + authorization middleware in the second pipeline.
5. **Unchanged, must stay green (backend)** — `UsersAuthorizationTests` (AC 4 on `/api/users`: Agent/Supervisor 403, Admin/SuperAdmin 200, no token 401), `UserManagementTests.Admin_CannotCreateASuperAdmin_Returns403` / `Admin_CannotDeactivateTheSuperAdmin_Returns403` (now via `HasPermission`), `SeedTests.Startup_SeedsRolesAndSuperAdmin` (AC 1), `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous`, `LayerDependencyTests`, `LocalizedTextCatalogTests`.
6. **Unit (frontend, new)** — `client/src/auth/permissions.test.ts`: `lists exactly the permissions of the server catalogue`.
7. **Component (frontend, new)** — `client/src/features/auth/Can.test.tsx`: `shows its content when the user has the permission`, `hides its content when the user lacks the permission`, `hides its content while the permissions are loading`.
8. **App level (frontend, new)** — `client/src/App.permissions.test.tsx` (AC 5): `hides the menu items an agent has no permission for`, `shows Reports but not Users to a supervisor`, `sends a user without permission from /users to the dashboard without calling the users API`, `opens /users for a user with the permission`.
9. **Component (frontend, modified)** — `client/src/pages/users/UsersPage.test.tsx`: mocks `@/api/auth`; new `lets a SuperAdmin manage SuperAdmins: row buttons and the SuperAdmin role`, `hides the SuperAdmin role from an Admin`, `shows an Admin no edit or deactivate button on SuperAdmin rows`.
10. **Modified for timing / types only** — `client/src/App.layout.test.tsx` (3 `findByRole` waits), `client/src/pages/dashboard/DashboardPage.test.tsx` (`permissions: []`), `client/src/test/fake-api.ts` (`me` option, `superAdminMe` / `agentMe` / `supervisorMe`).
11. **Guards (unchanged files)** — `translations.test.ts`, `no-hardcoded-text.test.ts`, `theme.test.ts` (new `.tsx` files use no colors / direction classes).
12. **Manual smoke** — Verification step 6.

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
2. **Backend tests:** in `server/` run `dotnet test` — **170 passed** (76 unit, 94 integration), 0 failed.
3. **Frontend tests:** in `client/` run `npm test` — **375 passed** in 21 files.
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed (the "chunks larger than 500 kB" message is a warning).
5. **Frontend lint:** in `client/` run `npm run lint` — exit code 0, no warnings, no errors.
6. **Manual smoke** (user-secrets from CRM-2 already set; no migration in this story):
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http`.
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173`, sign in as `admin@crm.local` → sidebar shows Dashboard, Tickets, Customers, Knowledge base, Reports, Users.
   - **Users → Add user**: create "Agent One" (`agent1@crm.local`, `Agent#Pass1`, Support agent) and "Office Admin" (`office1@crm.local`, `Admin#Pass1`, Administrator).
   - Private window, sign in as `agent1@crm.local` → sidebar: Dashboard, Tickets, Customers, Knowledge base only. Type `http://localhost:5173/users` → back on the dashboard. (DevTools Network: `GET /api/auth/me` → `"permissions":["customers.view","customers.manage","tickets.view","tickets.manage"]`.)
   - Sign out, sign in as `office1@crm.local` → Users visible; the "System Administrator" row has no Edit / Deactivate buttons; **Add user** dialog has no "System administrator" checkbox.
   - Click **العربية** → same items, Arabic labels, sidebar on the right.
7. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`, `client/src/components/ui/`, `client/src/i18n/`, `server/src/Crm.Infrastructure/Persistence/Migrations/`; `client/src/api/client.ts` is still the only `fetch` caller (`git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'`); `git grep -n "RequireRole" -- server/src` returns nothing.

---

## Done Criteria

- [ ] Roles SuperAdmin, Admin, Supervisor, Agent seeded (existing `Startup_SeedsRolesAndSuperAdmin`) and each has permissions (`EverySeededRole_HasPermissions`) — AC 1.
- [ ] Agent on an admin-settings permission (categories, SLA, channels, users) → 403 ProblemDetails (`AdminSettingsEndpoint_AsAgent_Returns403`, `UsersApi_AsAgent_Returns403`) — AC 2.
- [ ] SuperAdmin has every permission and is never 401/403 on any `/api/*` endpoint (`SuperAdmin_HasEveryPermission`, `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint`) — AC 3.
- [ ] Every protected `/api/*` endpoint requires a catalogue permission (`EveryProtectedApiEndpoint_RequiresAKnownPermission`); policies are permission requirements checked on the server (`PermissionAuthorizationHandler`); no `RequireRole` left — AC 4.
- [ ] Sidebar shows only permitted items and gated routes redirect to `/` (`App.permissions.test.tsx`); Admin sees no SuperAdmin role option / SuperAdmin row actions (`UsersPage.test.tsx`) — AC 5.
- [ ] `Permissions.cs` + `RolePermissions.cs` hold the full Phase 1 catalogue/matrix (table in this plan); `client/src/auth/permissions.ts` equals it (`permissions.test.ts`).
- [ ] `GET /api/auth/me` returns `permissions`; the JWT is unchanged.
- [ ] No new packages, no migration, no new UI strings, no hand edits in `client/src/components/ui/`.
- [ ] `dotnet build`, `dotnet test` (170), `npm test` (375), `npm run build`, `npm run lint` all pass.
- [ ] Committed on `feature/crm-7-roles-permissions` with message `CRM-7: roles and permissions`.
- [ ] `.squad/plans/02-security-admin/00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 08.**
