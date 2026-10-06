# Story 19 — SLA policy configuration (SuperAdmin) (Story: CRM-19)

## Prerequisites

- Foundation (CRM-1..5), security-admin (CRM-6, CRM-7), CRM-12 (ticket priorities) — on branch `feature/group-d-sla` (starts at `69a8a35`).
- **Binding notes from earlier plans:**
  - [../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md): `sla.manage` = **SuperAdmin only** (`RolePermissions`: Admin gets every permission except `users.manage-super-admins` and `sla.manage`); settings stories copy `AdminSettingsEndpoint_AsAgent_Returns403` against the real route; client route inside `RequirePermission`, actions behind `<Can>`.
  - [../04-ticket-management/12-story-ticket-categories-CRM-12.md](../04-ticket-management/12-story-ticket-categories-CRM-12.md) "How later stories build on this": *"CRM-19 (SLA): due times per `TicketPriority` value; do not add priorities."* API names `high` / `mid` / `low` via `TicketValues`.
  - CRM-8 entity / service conventions: Domain class with private setters + `DateTime utcNow` arguments, `IEntityTypeConfiguration<T>` in `Persistence/Configurations`, Application service + repository interface, Infrastructure repository, unit tests with a fake repository + hand-set `TimeProvider`.
- **No new NuGet / npm packages, no new shadcn component.** One migration: **`AddSlaPolicies`**.

---

## Story Goal

A SuperAdmin keeps one SLA policy per priority: the time allowed for the first response and the time allowed to resolve.

1. The migration seeds a policy for **High, Mid, Low** (AC 1): High 120 / 480 min (2 h / 8 h), Mid 240 / 1440 (4 h / 24 h), Low 480 / 4320 (8 h / 72 h).
2. `GET /api/sla-policies` → the three policies, High → Low: `{ priority: "high", responseMinutes, resolutionMinutes, updatedAt }`.
3. `PUT /api/sla-policies/{priority}` `{ responseMinutes: 60, resolutionMinutes: 240 }` → 200 + saved values; a later `GET` returns them (AC 2).
4. Both endpoints need `sla.manage`: Admin / Supervisor / Agent → **403** ProblemDetails (AC 3).
5. `responseMinutes` / `resolutionMinutes` missing, `<= 0`, or `resolutionMinutes < responseMinutes` → **400** with field errors (`responseMinutes` / `resolutionMinutes`) (AC 4). Unknown priority (`/api/sla-policies/urgent`) → 404.
6. Client: sidebar **"SLA policy"** (`/sla-policies`, only with `sla.manage`): table (priority, response time, resolution time formatted as "1 h", "1 h 30 min", "45 min"), "Edit" dialog with two minute inputs; all text in en + ar.

**Decisions**

- **Minutes as integers** in the API and the database (no `TimeSpan` JSON, exact compare in SQL for CRM-21). Upper bound 525 600 (one year) — keeps `DateTime.AddMinutes` sane.
- **Key = priority** (stored as string like `Ticket.Priority`, `HasConversion<string>()`): one row per priority, no surrogate id. Seed with `HasData` (anonymous objects, private setters) so `EnsureCreated` in tests seeds too.
- **24/7 clock**: no business hours (out of scope); `SlaPolicy.ResponseDueAt(createdAt)` / `ResolutionDueAt(createdAt)` = `createdAt + minutes` — used by CRM-20.
- Domain also guards the rules (`Update` throws `ArgumentOutOfRangeException`); the validator produces the 400 field errors first.
- Read also needs `sla.manage`: only the settings page reads the policy; tickets carry their own due times (CRM-20).

**Not in scope:** business hours / holidays, per-customer or per-category SLA, applying the policy to tickets (CRM-20), breaches (CRM-21), escalation (CRM-22).

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules, Architecture decisions, Frontend rules.
2. `.squad/stories/05-sla-automation/CRM-19/intake.md` — AC 1–4.
3. `server/src/Crm.Domain/Tickets/TicketCategory.cs` (entity style, `EnsureUtc`), `TicketPriority.cs`.
4. `server/src/Crm.Application/Tickets/TicketCategoryService.cs`, `ITicketCategoryRepository.cs`, `TicketCategoryText.cs`, `TicketValues.cs` (`TryParsePriority`, `PriorityName`).
5. `server/src/Crm.Infrastructure/Persistence/Configurations/TicketCategoryConfiguration.cs`, `Tickets/TicketCategoryRepository.cs`, `DependencyInjection.cs`, `CrmDbContext.cs`.
6. `server/src/Crm.Api/Endpoints/TicketCategoriesEndpoints.cs`, `Program.cs`.
7. `server/tests/Crm.Api.IntegrationTests/Tickets/TicketCategoriesAuthorizationTests.cs` (policy dictionary test), `Auth/PermissionPolicyTests.cs` (SuperAdmin never 401/403 on every route — PUT with `{}` must answer 400/404).
8. `server/tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs` (`TestClock`).
9. Client: `client/src/pages/ticket-categories/*`, `client/src/features/ticket-categories/*`, `client/src/api/ticket-categories.ts` (+ test), `client/src/app/navigation.ts`, `AppRoutes.tsx`, `App.layout.test.tsx` / `App.i18n.test.tsx` (navigation label lists), `client/src/no-hardcoded-text.test.ts`.

---

## Backend Tasks

All commands from `server/`.

### 1 — Unit tests first (Red)

**Create `server/tests/Crm.UnitTests/Sla/SlaPolicyTests.cs`** — `Defaults_CoverEveryPriority` (AC 1: `SlaPolicy.Defaults` has High, Mid, Low with resolution ≥ response > 0), `Update_SavesTheValues_AndMovesUpdatedAt`, `Update_WithZeroOrNegative_Throws`, `Update_WithResolutionBelowResponse_Throws`, `DueTimes_AddTheMinutesToTheStart`.

**Create `server/tests/Crm.UnitTests/Sla/SlaPolicyServiceTests.cs`** (fake repository in `Sla/SlaTestDoubles.cs`, `TestClock` reused from `Crm.UnitTests.Tickets`) — `List_ReturnsHighMidLow_WithApiNames`, `Update_High_To60And240_IsSaved` (AC 2), `Update_WithZero_ThrowsValidationException_OnTheField`, `Update_WithResolutionBelowResponse_ThrowsValidationException_OnResolutionMinutes`, `Update_WithMissingValues_ThrowsValidationException`, `Update_UnknownPriority_ThrowsNotFound` (AC 4).

Run `dotnet test tests/Crm.UnitTests` → Red (`Crm.Domain.Sla` missing).

### 2 — Domain + Application (Green)

**Create `server/src/Crm.Domain/Sla/SlaPolicy.cs`** — `MaxMinutes = 525_600`; `Priority` (`TicketPriority`), `ResponseMinutes`, `ResolutionMinutes`, `UpdatedAt` (private setters); `static SlaPolicy Create(TicketPriority, int response, int resolution, DateTime utcNow)`; `Update(int response, int resolution, DateTime utcNow)`; `static bool AreValid(int response, int resolution)`; `DateTime ResponseDueAt(DateTime startUtc)`, `ResolutionDueAt(DateTime startUtc)`; `static IReadOnlyList<(TicketPriority, int, int)> Defaults` and `DefaultsSeededAt = 2026-10-06T00:00:00Z`.

**Create `server/src/Crm.Application/Sla/`:**

- `SlaPolicyContracts.cs` — `UpdateSlaPolicyRequest(int? ResponseMinutes, int? ResolutionMinutes)`, `SlaPolicyResponse(string Priority, int ResponseMinutes, int ResolutionMinutes, DateTime UpdatedAt)`.
- `UpdateSlaPolicyRequestValidator.cs` — both `NotNull().GreaterThan(0).LessThanOrEqualTo(SlaPolicy.MaxMinutes)`; `ResolutionMinutes` `GreaterThanOrEqualTo(x => x.ResponseMinutes)` when both set, message `SlaText.ResolutionBelowResponse`; `WithName` from `SlaText`.
- `SlaText.cs` — `ResponseField` ("Response time" / "وقت الاستجابة"), `ResolutionField` ("Resolution time" / "وقت الحل"), `ResolutionBelowResponse` ("The resolution time must be at least the response time." / "يجب ألا يقل وقت الحل عن وقت الاستجابة."), `PolicyNotFound` ("There is no SLA policy for this priority." / "لا توجد سياسة SLA لهذه الأولوية.").
- `ISlaPolicyRepository.cs` — `ListAsync` (High → Low, not tracked), `FindAsync(TicketPriority)` (tracked), `SaveChangesAsync`.
- `ISlaPolicyService.cs` / `SlaPolicyService.cs` — `ListAsync`, `UpdateAsync(string priority, UpdateSlaPolicyRequest)`: parse with `TicketValues.TryParsePriority` (unknown → `NotFoundException(SlaText.PolicyNotFound)`), validate, `policy.Update`, save.
- `DependencyInjection.cs` — `services.AddScoped<ISlaPolicyService, SlaPolicyService>();`.

### 3 — Integration tests (Red)

**Create `server/tests/Crm.Api.IntegrationTests/Sla/SlaPoliciesTests.cs`** (+ `SlaBodies.cs`):
- `DefaultPolicies_AreSeeded_ForHighMidLow` (AC 1).
- `SuperAdmin_UpdatesHigh_To1hAnd4h_ValuesAreSaved` (AC 2; restores the default at the end — shared DB).
- `Update_WithZero_Returns400_WithTheField`, `Update_WithResolutionBelowResponse_Returns400`, `Update_WithoutBody_Returns400` (AC 4), `Update_UnknownPriority_Returns404`.

**Create `server/tests/Crm.Api.IntegrationTests/Sla/SlaPoliciesAuthorizationTests.cs`** — 401 without token (GET, PUT); `NonSuperAdmin_UpdatingSla_Gets403` theory Admin / Supervisor / Agent (AC 3) + GET 403; `SlaEndpoints_NeedSlaManage` (policy dictionary: `"GET /api/sla-policies/"`, `"PUT /api/sla-policies/{priority}"`, count 2).

### 4 — Infrastructure + Api (Green)

- `Persistence/Configurations/SlaPolicyConfiguration.cs` — table `SlaPolicies`, key `Priority` (`HasConversion<string>().HasMaxLength(10)`), `HasData` from `SlaPolicy.Defaults`.
- `CrmDbContext`: `public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();`.
- `Crm.Infrastructure/Sla/SlaPolicyRepository.cs` (orders by the enum value in memory — 3 rows) + registration.
- `Crm.Api/Endpoints/SlaPoliciesEndpoints.cs` — group `/api/sla-policies` `.RequireAuthorization(Permissions.SlaManage)`; `GET ""`, `PUT "/{priority}"`. `Program.cs`: `app.MapSlaPoliciesEndpoints();`.

### 5 — Migration

`dotnet ef migrations add AddSlaPolicies --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations` → `CreateTable("SlaPolicies")` + 3 `InsertData` rows. Never hand-edit.

---

## Frontend Tasks

All commands from `client/`.

### 1 — Tests first (Red)

- `client/src/api/sla-policies.test.ts` — `listSlaPolicies()` → `GET /api/sla-policies`; `updateSlaPolicy('high', { responseMinutes: 60, resolutionMinutes: 240 })` → `PUT /api/sla-policies/high` body.
- `client/src/features/sla/sla-format.test.ts` — `formatMinutes(45)` "45 min", `(60)` "1 h", `(90)` "1 h 30 min" (en) and Arabic variants.
- `client/src/pages/sla/SlaPoliciesPage.test.tsx` — lists High/Mid/Low with formatted times; edit High → 60 / 240 → `updateSlaPolicy('high', …)` + toast (AC 2); client blocks `0` and resolution < response before calling the API (AC 4); server 400 field message shown under the field; "Edit" hidden without `sla.manage`.
- `App.layout.test.tsx` `NAVIGATION_LABELS` + `'SLA policy'`; `App.i18n.test.tsx` `ARABIC_NAVIGATION_LABELS` + `'سياسة SLA'`.

### 2 — Implementation (Green)

- `client/src/api/sla-policies.ts` — `SlaPolicy`, `SlaPolicyRequest`, `listSlaPolicies(signal)`, `updateSlaPolicy(priority, request)`.
- `client/src/features/sla/` — `sla-format.ts` (`formatMinutes(minutes, t)`), `useSlaPolicies.ts` (key `['sla-policies']`), `sla-policy-form-schema.ts` (zod: ints 1..525600, `refine` resolution ≥ response on `resolutionMinutes`), `SlaPolicyFormDialog.tsx`, `SlaPoliciesTable.tsx`.
- `client/src/pages/sla/SlaPoliciesPage.tsx`.
- `navigation.ts`: `{ id: 'slaPolicies', path: '/sla-policies', icon: TimerIcon, permission: permissions.slaManage }` after `ticketCategories`; `AppRoutes.tsx`: route inside `RequirePermission permission={permissions.slaManage}`.
- `en.json` / `ar.json`: `nav.slaPolicies`, block `sla` (description, columns, edit, loading, editTitle/Description, responseMinutes, resolutionMinutes, save, saving, cancel, minutesRequired, minutesPositive, resolutionBelowResponse, updated, duration.{minutes,hours,hoursMinutes}).

---

## Edge Cases & Failure Modes

- `responseMinutes = 0` / negative / missing → 400 field error (validator; Domain throws as a second guard).
- Resolution == response → allowed (AC says ≥).
- `PUT /api/sla-policies/HIGH` → accepted (case-insensitive like `TicketValues`); `urgent` / `1` → 404.
- Admin (not SuperAdmin) → 403 on GET and PUT; client hides the sidebar item.
- Huge numbers (> one year) → 400 (keeps due-time math in range).

---

## Test Plan

1. Unit: `Crm.UnitTests/Sla/SlaPolicyTests.cs`, `SlaPolicyServiceTests.cs`.
2. Integration: `Crm.Api.IntegrationTests/Sla/SlaPoliciesTests.cs` (AC 1, 2, 4), `SlaPoliciesAuthorizationTests.cs` (AC 3); `PermissionPolicyTests` guards unchanged.
3. Client: `api/sla-policies.test.ts`, `features/sla/sla-format.test.ts`, `pages/sla/SlaPoliciesPage.test.tsx`, navigation label tests.

---

## Verification Steps

1. `cd server && dotnet build` → 0 warnings, 0 errors; `dotnet test` → green.
2. `cd client && npm test && npm run build && npm run lint` → green.

---

## Done Criteria

- [ ] AC 1: three policies seeded (migration `InsertData`, `EnsureCreated` in tests).
- [ ] AC 2: SuperAdmin PUT High 60 / 240 → saved and returned by GET.
- [ ] AC 3: Admin / Supervisor / Agent → 403.
- [ ] AC 4: ≤ 0, missing, resolution < response → 400 with field errors.
- [ ] SLA policy page only with `sla.manage`; strings in en + ar.
- [ ] build / tests / lint green.

---

## How later stories build on this (write nothing here; for later planners)

- **CRM-20 (timers):** on ticket create read the policy of the ticket priority (`ISlaPolicyRepository.FindAsync(priority)` or a read-only `ISlaPolicyProvider`) and store `ResponseDueAt = policy.ResponseDueAt(createdAt)`, `ResolutionDueAt = policy.ResolutionDueAt(createdAt)` **on the ticket** — changing the policy later does not move them (CRM-20 AC 4).
- **CRM-21 / CRM-22:** compare `UtcNow` to the ticket's stored due times; 80 % warning = `CreatedAt + 0.8 × (ResponseDueAt − CreatedAt)` (no policy read needed).
- Policy changes are not audited (no history table); add one if a later story asks.
