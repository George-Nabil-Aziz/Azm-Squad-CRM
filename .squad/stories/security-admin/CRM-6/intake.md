# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/security-admin/CRM-6/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Security & administration
- **Feature slug (folder under `plans/`):** `security-admin`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-6` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
User management
```

---

## Description

```
As an admin, I want to create, edit, and deactivate staff users, so that I control who can access the CRM.
```

---

## Acceptance criteria

```
1. Admin creates a user -> that user can log in.
2. Creating a user with an existing email returns 400.
3. A deactivated user trying to log in gets 401.
4. A non-admin calling the users API gets 403.
5. Users list supports search by name/email and pagination.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1 (project skeleton), CRM-5 (global error handling), CRM-2 (Identity + JWT, seeded roles, `admin@crm.local`), CRM-3 (shadcn/ui layout, routing, React Query; "Users" nav item → `ComingSoonPage`), CRM-4 (ar/en i18n, `<Feature>Text` server texts) — all done and merged to `main`.
- **Depends on code areas or other stories:** `ApplicationUser` / `UserManager<ApplicationUser>` / `CrmDbContext` + migrations (Infrastructure), `AuthService.LoginAsync`, JWT setup in `Crm.Api/Auth/AuthenticationExtensions.cs`, `CrmApiFactory`; client `client/src/api/client.ts`, `client/src/app/AppRoutes.tsx`, `client/src/i18n/{en,ar}.json`. Runs **before** CRM-7 (roles & permissions: permission-based policies + hiding menu items).

## Extra notes (optional)

- Phase 1 execution order: CRM-1 ✅, CRM-5 ✅, CRM-2 ✅, CRM-3 ✅, CRM-4 ✅, **CRM-6**, CRM-7, then customers (CRM-8..11), tickets (CRM-12..18), SLA (CRM-19..22), email/WhatsApp (CRM-23..26).
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.
- CRM-2 plan: CRM-6 manages `ApplicationUser` through `UserManager<ApplicationUser>` in Infrastructure (Application talks to it through an interface); a new user column (e.g. `IsActive`) comes with a new migration; `AuthService.LoginAsync` must then reject inactive users with the same 401 message.
- Existing tokens of a deactivated user should stop working too (decide how, and test it).
- "Admin" = roles `SuperAdmin` and `Admin` for now (role-based authorization). CRM-7 switches to permission policies.
- CRM-3 plan: CRM-6 replaces the `users` coming-soon route with the user-management page.

## Technical hints (optional)

- `CLAUDE.md` "Architecture decisions": minimal API endpoints in `Crm.Api/Endpoints/<Feature>Endpoints.cs` under `/api/users`; one service per feature (`I<Feature>Service`) with request/response DTO records and FluentValidation; failures as Application exceptions → ProblemDetails; pagination `page` (default 1) / `pageSize` (default 20, max 100) → `PagedResult<T>(Items, Page, PageSize, TotalCount)`.
- Integration tests on the shared `CrmApiFactory` (SQLite in-memory); tests that need another role create a user through `UserManager<ApplicationUser>`.
- Client: page in `client/src/pages/users/`, components in `client/src/features/users/`, shadcn/ui dialogs for create/edit, react-hook-form + zod, React Query, every string in `client/src/i18n/{en,ar}.json`, server texts in a `UserText` class.

## Out of scope

- Permission-based policies and hiding menu items / routes by permission (CRM-7).
- Password reset / change by the admin or the user, invitation emails, email confirmation, 2FA.
- Deleting users (deactivation only), audit log of user changes, per-user language preference.
