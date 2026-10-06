# security-admin — plan overview

Entry point for the **security-admin** feature (staff users, roles and permissions). Stories execute in order by their `NN` prefix; `NN` continues the global sequence after [foundation](../01-foundation/00-overview.md) (01–05).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 06 | [06-story-user-management-CRM-6.md](06-story-user-management-CRM-6.md) | User management | CRM-6 | 01–05 (foundation) |
| 07 | [07-story-roles-permissions-CRM-7.md](07-story-roles-permissions-CRM-7.md) | Roles & permissions | CRM-7 | 03, 04, 05, 06 |
| 34 | [34-story-audit-logs-CRM-34.md](34-story-audit-logs-CRM-34.md) | Audit logs | CRM-34 | 06, 07, 19 (Phase 2) |
| 35 | [35-story-system-configuration-CRM-35.md](35-story-system-configuration-CRM-35.md) | System configuration | CRM-35 | 07, 19, 23..26, 34 (Phase 2) |

## Dependency notes

- **Every story here builds on the whole foundation feature** ([../01-foundation/00-overview.md](../01-foundation/00-overview.md)): ProblemDetails + Application exceptions (CRM-5), Identity / JWT / seeded roles / `CrmApiFactory` (CRM-2), shadcn layout + routes + React Query (CRM-3), ar/en i18n + `<Feature>Text` server texts (CRM-4).
- **Story 06 (CRM-6)** adds user management and the shared pieces later stories reuse: `ApplicationUser.IsActive` + migration `AddUserIsActive`; `AuthService` refuses inactive users; JWT `OnTokenValidated` rejects tokens of inactive / deleted users through `IActiveUserChecker` (one DB lookup per authenticated request); `ICurrentUser` (Application) / `HttpCurrentUser` (Api); `PagedResult<T>` + `PagingDefaults` in `Crm.Application/Common/Paging`; named authorization policies in `Crm.Api/Auth/CrmPolicies.cs` (`ManageUsers` = roles SuperAdmin + Admin, defined in `AddCrmAuthentication`); `/api/users` endpoints; `CrmApiFactory.CreateUserAsync` / `CreateClientWithRoleAsync`; client `client/src/api/users.ts` (`roleNames`, `PagedResult`), `apiPut`, the users page, shadcn `table`, `dialog`, `checkbox`, `badge`, `alert-dialog`, and the `ResizeObserver` stub in `client/src/test/setup.ts`.
- **CRM-7 (roles & permissions)** changes only the policy definitions in `AddCrmAuthentication` (endpoints keep `CrmPolicies.*` names), filters `navigationItems` by permission, and may hide the SuperAdmin role option in the users dialog for non-SuperAdmins (the API already refuses it with 403).
- **Story 07 (CRM-7)** adds the permission model every later story uses: the Phase 1 permission catalogue `Crm.Application/Auth/Permissions.cs` and the role matrix `RolePermissions.cs` (table in [07-story-roles-permissions-CRM-7.md](07-story-roles-permissions-CRM-7.md)), one authorization policy per permission (policy name = permission value, `PermissionAuthorizationHandler` reads the `role` claims — permissions are not in the token), `ICurrentUser.HasPermission`, `permissions` in `GET /api/auth/me`, the guards `EveryProtectedApiEndpoint_RequiresAKnownPermission` and `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint`, test endpoints `/_test/permissions/<permission>`; client `client/src/auth/permissions.ts` (kept equal to the server by `permissions.test.ts`), `usePermissions()`, `<Can>`, `<RequirePermission>` route guard, `NavigationItem.permission`, `fakeApi({ me })` with `superAdminMe` / `agentMe` / `supervisorMe`. `CrmPolicies.ManageUsers` is now an alias of `Permissions.UsersManage`. New endpoints call `RequireAuthorization(Permissions.X)`; new sidebar areas set `permission` and wrap their route in `RequirePermission`.
