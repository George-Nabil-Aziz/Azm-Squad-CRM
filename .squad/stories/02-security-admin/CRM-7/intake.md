# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/02-security-admin/CRM-7/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Security & administration
- **Feature slug (folder under `plans/`):** `02-security-admin`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-7` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Roles & permissions
```

---

## Description

```
As a SuperAdmin, I want role-based permissions (SuperAdmin, Admin, Supervisor, Agent), so that each user only does what their role allows.
```

---

## Acceptance criteria

```
1. Roles SuperAdmin, Admin, Supervisor, Agent are seeded.
2. An Agent calling an admin settings endpoint gets 403.
3. A SuperAdmin can access every endpoint.
4. Permissions are enforced on the API, not only hidden in the UI.
5. The UI hides menu items the user has no permission for.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1, CRM-5, CRM-2, CRM-3, CRM-4, CRM-6 — all done and merged to `main`.
- **Depends on code areas or other stories:** seeded roles + `Roles` constants (CRM-2, `Crm.Application/Auth/Roles.cs`, `CrmDbInitializer`), JWT `role` claims (`JwtAccessTokenGenerator`, `RoleClaimType = "role"`), `GET /api/auth/me` (`AuthEndpoints`), the named policy `CrmPolicies.ManageUsers` defined once in `AddCrmAuthentication` (CRM-6), `ICurrentUser` / `UserService.EnsureMayManage` (SuperAdmin-only rule, CRM-6), guard test `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous` (CRM-2); client `navigationItems` (`client/src/app/navigation.ts`), `AppSidebar`, `AppRoutes`, `useCurrentUser`, users dialog/table (CRM-6).

## Extra notes (optional)

- Phase 1 execution order: CRM-1 ✅, CRM-5 ✅, CRM-2 ✅, CRM-3 ✅, CRM-4 ✅, CRM-6 ✅, **CRM-7**, then customers (CRM-8..11), tickets (CRM-12..18), SLA (CRM-19..22: SLA policy configuration is SuperAdmin-only), email/WhatsApp (CRM-23..26).
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.
- Permission model is **code-defined** (permission constants mapped to the 4 seeded roles); no permission-editing UI.
- Define the permission catalogue for the **whole Phase 1** now (users, customers, tickets incl. assign, categories, SLA settings, channel settings) so later stories only reference constants; document it in the plan for later planners.
- CRM-6 plan: endpoints keep `CrmPolicies.*` names; only the policy definition in `AddCrmAuthentication` changes. CRM-6 left two UI items to CRM-7: hide the "Users" menu item / route for non-admins, hide the SuperAdmin role option from Admins.
- SuperAdmin can access everything. An architecture-style test that every non-anonymous endpoint requires a permission is welcome.

## Technical hints (optional)

- Permissions exposed to the client through `GET /api/auth/me` (or token claims — the plan decides); client `usePermissions` hook + `<Can>` helper; sidebar items and routes filtered by permission (UI hiding only; the API enforces).
- Server texts / client strings follow CRM-4 (both `en`/`ar`), integration tests on `CrmApiFactory` (`CreateClientWithRoleAsync`).

## Out of scope

- Editing roles or permissions at runtime (UI or API), custom roles, per-user permission overrides.
- The real admin-settings endpoints (categories CRM-12, SLA CRM-19, channels CRM-23..26) — this story only defines their permissions.
- Pushing role changes into already-issued tokens (still applied at next login, ≤ 60 min, as decided in CRM-6).
- Audit log of permission-denied attempts.
