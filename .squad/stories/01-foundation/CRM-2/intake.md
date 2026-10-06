# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/01-foundation/CRM-2/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Foundation
- **Feature slug (folder under `plans/`):** `01-foundation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-2` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Authentication (login with JWT)
```

---

## Description

```
As a staff user, I want to log in with email and password, so that only authorized people can use the CRM.
```

---

## Acceptance criteria

```
1. Valid email + password returns 200 with a JWT access token.
2. Wrong password returns 401 with no token.
3. Calling a protected endpoint without a token returns 401.
4. Calling a protected endpoint with a valid token returns 200.
5. An expired token returns 401.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1 (project skeleton) and CRM-5 (global error handling) — both done and merged to `main`.
- **Depends on code areas or other stories:** Builds on `server/` (`Crm.Api`, `Crm.Application`, `Crm.Infrastructure`, `CrmApiFactory` test host, `GlobalExceptionHandler`) and `client/` (`client/src/api/client.ts`, `ApiErrorToaster`) from CRM-1/CRM-5. Runs **before** CRM-3 (layout + shadcn/ui + styled login page), CRM-4 (ar/en + RTL), CRM-6 (user management), CRM-7 (roles & permissions).

## Extra notes (optional)

- Phase 1 execution order: CRM-1 ✅, CRM-5 ✅, **CRM-2**, CRM-3, CRM-4, CRM-6, CRM-7, then customers, tickets, SLA, email/WhatsApp.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.
- CRM-5 left the client handling of 401 to this story.

## Technical hints (optional)

- `CLAUDE.md` "Architecture decisions": `CrmDbContext` in `Crm.Infrastructure/Persistence/` derives from `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`; Identity types live in Infrastructure; migrations in `Crm.Infrastructure/Persistence/Migrations`; SQL Server LocalDB in Development (`ConnectionStrings:Crm`), migrations applied at startup in Development only.
- Integration tests: shared `CrmApiFactory`, environment `Testing`, SQLite in-memory (one open connection, `EnsureCreated`), JWT key + seed admin password injected with `ConfigureAppConfiguration`; fake `TimeProvider` for time control.
- Secrets `Jwt:SigningKey`, `Seed:SuperAdminPassword` via `dotnet user-secrets` / environment variables, never in committed appsettings.
- Seed: roles `SuperAdmin`, `Admin`, `Supervisor`, `Agent` and SuperAdmin user `admin@crm.local`.
- Client: token kept by `client/src/auth/` and attached by `client/src/api/client.ts`; components never read the token.

## Out of scope

- Styled login page, app layout, routing and redirects (CRM-3) — this story ships a minimal unstyled sign-in form only.
- Arabic / RTL text (CRM-4) — temporary English strings in one module.
- User management screens/endpoints (CRM-6), permissions and role-based policies (CRM-7).
- Refresh tokens, "remember me", password reset, email confirmation, 2FA, external logins.
