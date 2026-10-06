# Story 12 — Ticket categories & priorities (Story: CRM-12)

## Prerequisites

- Foundation (CRM-1..5), security-admin (CRM-6, CRM-7) and customer-management (CRM-8, CRM-9) merged to `main` ([../03-customer-management/00-overview.md](../03-customer-management/00-overview.md)).
- **Binding notes from earlier plans:**
  - [../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md) lines 40–56 (catalogue: `categories.manage` = SuperAdmin + Admin; `tickets.view` / `tickets.manage` = every staff role) and section 5 lines 671–681 ("Settings stories (CRM-12 categories …)": copy `AdminSettingsEndpoint_AsAgent_Returns403` against the real route; client route inside `RequirePermission`, actions behind `<Can>`; page tests mock `@/api/auth`; gated items appear after `/api/auth/me` → `findByRole`).
  - [../03-customer-management/08-story-customer-profiles-CRM-8.md](../03-customer-management/08-story-customer-profiles-CRM-8.md) lines 1573–1581 ("Every new entity": Domain class with private setters + `Create` taking `DateTime utcNow`; mapping in `Persistence/Configurations/<Entity>Configuration.cs`; only a `DbSet` added to `CrmDbContext`; one migration per story. "Every new feature service without Identity": Application `I<Feature>Service` + `<Feature>Service`, `I<Feature>Repository` in `Crm.Infrastructure/<Feature>/`; unit-test the service with a fake repository + hand-set `TimeProvider`).
  - [../03-customer-management/09-story-customer-contacts-CRM-9.md](../03-customer-management/09-story-customer-contacts-CRM-9.md) lines 2368–2374 (API enum values are lower-case names like contact types `"phone"`; `ContactValues` is the precedent for name ↔ enum mapping).
- Branch **`feature/group-b-tickets`** (Group B: CRM-12, CRM-13, CRM-14 in this order). CRM-10 / CRM-11 are built in parallel on another branch — do not touch their files.
- **No new NuGet / npm packages, no new shadcn component** (`table`, `dialog`, `field`, `input`, `checkbox`, `badge`, `button` exist). One migration: **`AddTicketCategories`**.

---

## Story Goal

Admins keep a list of ticket categories; every ticket uses one of the fixed priorities High / Mid / Low.

1. `POST /api/ticket-categories` `{ name }` (needs `categories.manage`) → **201** + `Location` + `{ id, name, isActive: true, createdAt, updatedAt }`. The category is then returned by `GET /api/ticket-categories?activeOnly=true`, the list the new-ticket form (CRM-13) reads (AC 1).
2. A name that already exists (trimmed, **case-insensitive**) → **400** ProblemDetails with `errors.name` (AC 2) — on create and on rename.
3. `PUT /api/ticket-categories/{id}` `{ name, isActive }` renames and (de)activates. An inactive category is **not** in `?activeOnly=true` (not selectable for new tickets) but stays in the full list with `isActive: false`, so tickets that already use it keep showing it (AC 3). No delete endpoint.
4. Priorities are a fixed enum `TicketPriority { High, Mid, Low }` with API names `"high"`, `"mid"`, `"low"` (server `TicketValues`, client `ticketPriorities`), translated in both languages; CRM-13 makes the priority required on every ticket (AC 4).
5. Reads (`GET`) need `tickets.view` (every staff role — the new-ticket form and the CRM-14 category filter use it); writes need `categories.manage` too (Agent / Supervisor → 403).
6. Client: sidebar item **"Ticket categories"** (`/ticket-categories`, only with `categories.manage`): table (name, status), "Add category", "Edit" dialog with an "Active" checkbox; all text in English and Arabic.

**Decisions**

- **Uniqueness via `NormalizedName`** (trimmed, `ToUpperInvariant()`, set by the Domain) with a **unique index**: works the same on SQL Server and SQLite (SQLite `=` is case-sensitive). The service checks first and throws `ValidationException` (`name`) — the AC wants 400, not 409.
- **Read permission = `tickets.view`** (not `tickets.manage` as the CRM-7 table hints): the CRM-14 filter bar needs category names for agents who only view tickets. Every role with `tickets.manage` has `tickets.view`, so the new-ticket form is unaffected.
- **Unpaged list** ordered by name: a short lookup list (CLAUDE.md pagination applies to growing lists). `activeOnly` (bool, default false).
- **`isActive` in the request is optional**: create → active when null; update → unchanged when null.
- Priorities are **not** stored in a table (fixed by the story, SLA hangs off the enum in CRM-19). No endpoint: the enum + client constant are the contract (a server unit test and a client test pin `high`/`mid`/`low`).
- Admin UI is its own sidebar area (there is no settings area yet); a later settings story may regroup it.

**Not in scope:** deleting categories, sub-categories, per-category SLA/routing, translated category names, the ticket entity / new-ticket form (CRM-13), the ticket list (CRM-14).

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules, Architecture decisions (Persistence, Endpoints, Application layer, Soft delete), Frontend rules.
2. `.squad/stories/04-ticket-management/CRM-12/intake.md` — AC 1–4, out of scope.
3. `server/src/Crm.Domain/Customers/Customer.cs` lines 13–56 (private ctor, private setters, `Create(…, DateTime utcNow)`, `EnsureUtc` lines 223–229) — copy the style.
4. `server/src/Crm.Application/Customers/CustomerService.cs` lines 295–331 (validate → domain → repository → response; `UtcNow()` line 398) and `ICustomerRepository.cs` lines 436–457.
5. `server/src/Crm.Application/Customers/ContactValues.cs` lines 22–27 (`TypesByName` dictionary, `OrdinalIgnoreCase`) — pattern for `TicketValues`.
6. `server/src/Crm.Application/Customers/CustomerText.cs` (text class style), `server/src/Crm.Application/Common/Exceptions/ValidationException.cs` (field → messages dictionary).
7. `server/src/Crm.Application/DependencyInjection.cs` line 13 (register `ITicketCategoryService` after it); `server/src/Crm.Infrastructure/DependencyInjection.cs` line 40 (register the repository after it).
8. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` line 17 (`DbSet<Customer>`; add `TicketCategories` after it), `Persistence/Configurations/CustomerConfiguration.cs` (configuration style), `server/src/Crm.Infrastructure/Customers/CustomerRepository.cs`.
9. `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs` lines 8–44 (group + `RequireAuthorization`, `Results.Created`, `.WithName`), `server/src/Crm.Api/Program.cs` line 34 (`app.MapCustomersEndpoints();` — map the new endpoints after it).
10. `server/tests/Crm.Api.IntegrationTests/Customers/CustomersAuthorizationTests.cs` (401 / 403 theory + the policy dictionary test), `Auth/PermissionPolicyTests.cs` lines 66–78 (`AdminSettingsEndpoint_AsAgent_Returns403`), `Infrastructure/CrmApiFactory.cs` lines 95–100 (`CreateClientWithRoleAsync`).
11. `server/tests/Crm.UnitTests/Customers/CustomerServiceTests.cs` lines 211–260 (private fake repository + `TestClock`) — write own doubles in `Crm.UnitTests/Tickets/` (do **not** share CRM-10's `TimelineTestDoubles.cs`).
12. Client: `client/src/app/navigation.ts` lines 26–33, `client/src/app/AppRoutes.tsx` lines 18–31, `client/src/pages/customers/CustomersPage.tsx`, `client/src/features/customers/CustomerFormDialog.tsx` (dialog + server field errors), `client/src/features/customers/useCustomers.ts`, `client/src/api/customers.ts` + `customers.test.ts` (API test style), `client/src/pages/customers/CustomersPage.test.tsx` lines 1–75, `client/src/App.layout.test.tsx` line 7 (`NAVIGATION_LABELS`), `client/src/no-hardcoded-text.test.ts` (comments are scanned too), `client/src/i18n/en.json` lines 3–15 (`nav`) and 105–173 (`customers`).

---

## Backend Tasks

All commands from `server/`.

### 1 — Unit tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Tickets/TicketCategoryTests.cs`** — `Create_TrimsTheName_IsActive_AndSetsNormalizedName`, `Create_WithABlankName_Throws`, `Create_WithANonUtcTime_Throws`, `Update_RenamesAndDeactivates_AndMovesUpdatedAt`.

**Create file: `server/tests/Crm.UnitTests/Tickets/TicketValuesTests.cs`** — `Priorities_AreExactlyHighMidLow` (`Enum.GetValues<TicketPriority>()` = High, Mid, Low; `TicketValues.PriorityNames` = `["high","mid","low"]`), `TryParsePriority_AcceptsNamesInAnyCase_RejectsOthers` (`"HIGH"` ok, `"1"`, `""`, `"urgent"` rejected).

**Create file: `server/tests/Crm.UnitTests/Tickets/TicketCategoryServiceTests.cs`** (fake repository + clock in **`server/tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs`**: `TestClock : TimeProvider`, `FakeTicketCategoryRepository`) — `Create_SavesAnActiveCategory`, `Create_WithADuplicateName_IgnoringCaseAndSpaces_ThrowsValidationException_OnName` (AC 2), `Create_WithoutAName_ThrowsValidationException`, `Update_ToTheNameOfAnotherCategory_ThrowsValidationException`, `Update_KeepingItsOwnName_IsAllowed`, `Update_Deactivates_WhenIsActiveFalse_AndKeepsStateWhenNull`, `Update_UnknownId_ThrowsNotFound`, `List_ActiveOnly_PassesTheFlagToTheRepository`.

Run `dotnet test tests/Crm.UnitTests` → **Red** (`CS0234 'Tickets' does not exist`).

### 2 — Domain + Application (Green)

**Create file: `server/src/Crm.Domain/Tickets/TicketPriority.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>Fixed ticket priorities (CRM-12). SLA due times are set per priority (CRM-19). Stored by name.</summary>
public enum TicketPriority
{
    High = 1,
    Mid = 2,
    Low = 3,
}
```

**Create file: `server/src/Crm.Domain/Tickets/TicketCategory.cs`** — `NameMaxLength = 100`; `Id`, `Name`, `NormalizedName`, `IsActive`, `CreatedAt`, `UpdatedAt` (private setters); `static TicketCategory Create(string name, DateTime utcNow)`; `void Update(string name, bool isActive, DateTime utcNow)`; `static string NormalizeName(string name) => name.Trim().ToUpperInvariant()`; blank name → `ArgumentException`; non-UTC → `ArgumentException` (same `EnsureUtc` as `Customer`). Not `ISoftDeletable` (deactivated, never deleted).

**Create folder `server/src/Crm.Application/Tickets/`:**

- `TicketValues.cs` — `PriorityNames` (`["high","mid","low"]`), `TryParsePriority(string?, out TicketPriority)` (dictionary `OrdinalIgnoreCase`, like `ContactValues.TypesByName`), `PriorityName(TicketPriority)`.
- `TicketCategoryContracts.cs` — `ListTicketCategoriesQuery(bool? ActiveOnly)`, `TicketCategoryRequest(string? Name, bool? IsActive)`, `TicketCategoryResponse(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt)`.
- `TicketCategoryRequestValidator.cs` — `Name` `NotEmpty().MaximumLength(TicketCategory.NameMaxLength).WithName(_ => TicketCategoryText.NameField)`.
- `TicketCategoryText.cs` — `NameField` ("Name" / "الاسم"), `NameTaken` ("A category with this name already exists." / "توجد فئة بهذا الاسم بالفعل."), `NotFound` ("The category was not found." / "الفئة غير موجودة.").
- `ITicketCategoryRepository.cs` — `ListAsync(bool activeOnly, ct)` (ordered by name), `FindAsync(Guid id, ct)` (tracked), `NameExistsAsync(string normalizedName, Guid? exceptId, ct)`, `Add`, `SaveChangesAsync`.
- `ITicketCategoryService.cs` / `TicketCategoryService.cs` — `ListAsync`, `CreateAsync`, `UpdateAsync`. Duplicate → `throw new ValidationException(new Dictionary<string, string[]> { ["name"] = [TicketCategoryText.NameTaken] })`; unknown id → `NotFoundException(TicketCategoryText.NotFound)`.

**File: `server/src/Crm.Application/DependencyInjection.cs`** — after line 13: `services.AddScoped<ITicketCategoryService, TicketCategoryService>();`.

### 3 — Integration tests (Red)

**Create file: `server/tests/Crm.Api.IntegrationTests/Tickets/TicketCategoriesTests.cs`** (+ `TicketBodies.cs` with `TicketCategoryBody`):

- `AdminCreatesACategory_ItIsListedAsActive_ForAnAgent` (AC 1: Admin POST → 201 + `Location`; Agent `GET /api/ticket-categories?activeOnly=true` contains it).
- `CreatingADuplicateName_Returns400_WithTheNameField` (AC 2: `"  BILLING x "` vs `"billing X"` → 400, `errors.name`).
- `RenamingToAnExistingName_Returns400`.
- `DeactivatedCategory_IsNotInTheActiveList_ButStaysInTheFullList` (AC 3).
- `Update_UnknownCategory_Returns404`; `Create_WithoutAName_Returns400`.

**Create file: `server/tests/Crm.Api.IntegrationTests/Tickets/TicketCategoriesAuthorizationTests.cs`** — 401 without token (GET, POST, PUT); `AgentAndSupervisor_CannotCreateOrUpdate_Return403`; `Agent_CanReadCategories`; `CategoryEndpoints_NeedTicketsViewToRead_AndCategoriesManageToWrite` (policy dictionary, keys `"GET /api/ticket-categories/"`, `"POST /api/ticket-categories/"`, `"PUT /api/ticket-categories/{id:guid}"`, count 3).

### 4 — Infrastructure + Api (Green)

- **Create file: `server/src/Crm.Infrastructure/Persistence/Configurations/TicketCategoryConfiguration.cs`** — table `TicketCategories`, `Id` `ValueGeneratedNever`, `Name`/`NormalizedName` max 100 required, `HasIndex(NormalizedName).IsUnique()`.
- **File: `CrmDbContext.cs`** after line 17: `public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();`.
- **Create file: `server/src/Crm.Infrastructure/Tickets/TicketCategoryRepository.cs`**; register after line 40 of `Crm.Infrastructure/DependencyInjection.cs`.
- **Create file: `server/src/Crm.Api/Endpoints/TicketCategoriesEndpoints.cs`** — `MapTicketCategoriesEndpoints()`: group `/api/ticket-categories` `.RequireAuthorization(Permissions.TicketsView)`; `GET ""` (`[AsParameters] ListTicketCategoriesQuery`), `POST ""` → `Results.Created($"/api/ticket-categories/{id}", …)`, `PUT "/{id:guid}"` — both writes `.RequireAuthorization(Permissions.CategoriesManage)`.
- **File: `Program.cs`** after line 34: `app.MapTicketCategoriesEndpoints();`.

### 5 — Migration

`dotnet ef migrations add AddTicketCategories --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations` → only `CreateTable("TicketCategories")` + unique index. Never hand-edit.

---

## Frontend Tasks

All commands from `client/`.

### 1 — Tests first (Red)

- **Create file: `client/src/api/ticket-categories.test.ts`** — `listTicketCategories({})` → `GET /api/ticket-categories`; `{ activeOnly: true }` → `?activeOnly=true`; `createTicketCategory` → POST body; `updateTicketCategory` → `PUT /api/ticket-categories/{id}` body.
- **Create file: `client/src/features/tickets/ticket-values.test.ts`** — `ticketPriorities` equals `['high','mid','low']` and every priority has an English and Arabic label (`tickets.priorities.*`).
- **Create file: `client/src/pages/ticket-categories/TicketCategoriesPage.test.tsx`** (mocks `@/api/auth` + `@/api/ticket-categories`) — lists categories with "Active"/"Inactive"; adds a category (dialog "Add category", label "Name", button "Save" → `createTicketCategory({ name, isActive: true })`); shows the server's duplicate-name message under the field on 400 (AC 2); edit dialog unchecks "Active" → `updateTicketCategory(id, { name, isActive: false })` (AC 3); "Add category" hidden without `categories.manage`.
- **File: `client/src/App.layout.test.tsx`** line 7 — `NAVIGATION_LABELS` gets `'Ticket categories'` at the end (SuperAdmin sees it). `App.permissions.test.tsx` stays unchanged (Agent / Supervisor do not see it).
- **Deviation:** `client/src/App.i18n.test.tsx` line 7 (`ARABIC_NAVIGATION_LABELS`) also gets `'فئات التذاكر'` — the Arabic sidebar test lists every item too.

### 2 — Implementation (Green)

- **Create `client/src/api/ticket-categories.ts`** — `TicketCategory`, `TicketCategoryRequest { name; isActive }`, `listTicketCategories({ activeOnly }, signal)`, `createTicketCategory`, `updateTicketCategory`.
- **Create `client/src/features/tickets/ticket-values.ts`** — `export const ticketPriorities = ['high', 'mid', 'low'] as const`, `type TicketPriority`.
- **Create `client/src/features/ticket-categories/`** — `useTicketCategories.ts` (query key `['ticket-categories', params]`, prefix `ticketCategoriesQueryKey`), `category-form-schema.ts` (zod: name trimmed 1–100), `TicketCategoryFormDialog.tsx` (create / edit; edit shows the "Active" checkbox; 400 → field errors like `CustomerFormDialog`), `TicketCategoriesTable.tsx` (name, status badge, "Edit" behind `<Can categoriesManage>`).
- **Create `client/src/pages/ticket-categories/TicketCategoriesPage.tsx`**.
- **File: `client/src/app/navigation.ts`** — add `{ id: 'ticketCategories', path: '/ticket-categories', icon: TagsIcon, permission: permissions.categoriesManage }` after `users`.
- **File: `client/src/app/AppRoutes.tsx`** — `<Route element={<RequirePermission permission={permissions.categoriesManage} />}><Route path="ticket-categories" element={<TicketCategoriesPage />} /></Route>` after the users route.
- **Files: `client/src/i18n/en.json` + `ar.json`** — `nav.ticketCategories`, block `ticketCategories` (description, add, columns, active, inactive, edit, empty, loading, createTitle/Description, editTitle/Description, name, isActive, save, saving, cancel, nameRequired, created, updated), block `tickets.priorities` (`high` "High" / "عالية", `mid` "Mid" / "متوسطة", `low` "Low" / "منخفضة").

---

## Edge Cases & Failure Modes

- **Same name, other case / spaces** (`" Billing "` vs `"billing"`) → 400 `errors.name` — `TicketCategory.NormalizeName` + `TicketCategoryService` duplicate check.
- **Rename to its own name in another case** → allowed (`NameExistsAsync(…, exceptId: id)`).
- **Two admins create the same name at the same moment** → the unique index on `NormalizedName` rejects the second insert (`DbUpdateException` → 500). Rare admin action; accepted, not handled.
- **Deactivated category on old tickets** → the row stays; the full list (and ticket responses in CRM-13) still show its name.
- **Agent calls POST/PUT** → 403 ProblemDetails (policy `categories.manage`).
- **`isActive` missing** on update → state unchanged; on create → active.
- **Blank or > 100 character name** → 400 (validator) before the duplicate check.

---

## Test Plan

1. Unit: `Crm.UnitTests/Tickets/TicketCategoryTests.cs`, `TicketValuesTests.cs`, `TicketCategoryServiceTests.cs` (doubles in `TicketTestDoubles.cs`).
2. Integration: `Crm.Api.IntegrationTests/Tickets/TicketCategoriesTests.cs` (AC 1–3), `TicketCategoriesAuthorizationTests.cs`; existing guards `PermissionPolicyTests` / `SoftDeleteModelTests` pass unchanged.
3. Client: `api/ticket-categories.test.ts`, `features/tickets/ticket-values.test.ts` (AC 4), `pages/ticket-categories/TicketCategoriesPage.test.tsx` (AC 1–3 UI), `App.layout.test.tsx` (navigation labels).

---

## Verification Steps

1. **Backend builds:** `cd server && dotnet build` → 0 warnings, 0 errors.
2. **Backend tests:** `cd server && dotnet test` → all green.
3. **Frontend:** `cd client && npm test && npm run build && npm run lint` → all green.
4. **Regression:** `PermissionPolicyTests` (every endpoint has a known permission, SuperAdmin never 401/403), `no-hardcoded-text.test.ts`, `translations.test.ts`.

---

## Done Criteria

- [ ] AC 1: an admin-created category is returned by `GET /api/ticket-categories?activeOnly=true` to an agent (integration test) — the CRM-13 form reads this list.
- [ ] AC 2: duplicate name (any case / spacing) → 400 with `errors.name`, on create and rename.
- [ ] AC 3: inactive categories are excluded from `activeOnly` and kept in the full list.
- [ ] AC 4: `TicketPriority` = High, Mid, Low; API names `high`/`mid`/`low`; client labels in en + ar.
- [ ] Writes need `categories.manage` (403 for Agent / Supervisor), reads `tickets.view`.
- [ ] Ticket categories page reachable from the sidebar only with `categories.manage`; all strings in en + ar.
- [ ] `dotnet build` / `dotnet test` / `npm test` / `npm run build` / `npm run lint` green.

---

## How later stories build on this (write nothing here; for later planners)

- **CRM-13 (create ticket):** `Ticket.Priority` is a required `TicketPriority` (stored as string, `HasConversion<string>()`), `Ticket.CategoryId` a nullable FK to `TicketCategories` (`Restrict`). The service rejects an unknown **or inactive** category with 400 `errors.categoryId` (AC 3 "not selectable for new tickets"), and responses show the category name even when it was deactivated later. The new-ticket form fills its category select from `listTicketCategories({ activeOnly: true })` and its priority select from `ticketPriorities` + `tickets.priorities.*`. Parse priorities with `TicketValues.TryParsePriority`.
- **CRM-14 (list filters):** the category filter uses `listTicketCategories({})` (inactive ones included, so old tickets can be filtered).
- **CRM-17 (priority/category change):** reuse the same "active category only" rule for changes.
- **CRM-19 (SLA):** due times per `TicketPriority` value; do not add priorities.
