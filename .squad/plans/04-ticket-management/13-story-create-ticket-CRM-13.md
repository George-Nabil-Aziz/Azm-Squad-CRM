# Story 13 — Create ticket (Story: CRM-13)

## Prerequisites

- Story 12 completed: [12-story-ticket-categories-CRM-12.md](12-story-ticket-categories-CRM-12.md) (CRM-12, same branch) — its "How later stories build on this" is **binding**: `Ticket.Priority` is a required `TicketPriority` stored as a string; `Ticket.CategoryId` is a nullable FK to `TicketCategories` (`Restrict`); an unknown **or inactive** category → 400 `errors.categoryId`; the response shows the category name even after it was deactivated; the form reads `listTicketCategories({ activeOnly: true })` and `ticketPriorities`; priorities are parsed with `TicketValues.TryParsePriority`.
- [../03-customer-management/08-story-customer-profiles-CRM-8.md](../03-customer-management/08-story-customer-profiles-CRM-8.md) lines 1576–1578 are **binding**: `Ticket.CustomerId` is a required FK with `OnDelete(DeleteBehavior.Restrict)`; queries that join customers use `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])` so tickets of soft-deleted customers stay visible (test "a ticket of a soft-deleted customer … shows its customer"); creating a ticket for a deleted customer fails because the customer is not found through the filter; the picker reuses `listCustomers({ search })`. Also "Every new entity" (UTC `DateTime`, private setters, `Create(…, utcNow)`, configuration class, one migration).
- [../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md) lines 46–47 (`tickets.view` reads, `tickets.manage` creates; every staff role has both) and lines 673–680 (route stays inside the existing `RequirePermission`; replace only the `ComingSoonPage` element; actions behind `<Can>`).
- CRM-10 (customer interaction timeline) is developed in parallel on another branch — **do not** record a timeline entry in this story; see section "Follow-up after CRM-10 merges".
- **One new shadcn component: `textarea`** (`npx shadcn@latest add textarea`, never hand-edited). No new packages. One migration: **`AddTickets`**.

---

## Story Goal

Agents create a ticket for a customer from the Tickets page; the API gives it the next human number.

1. `POST /api/tickets` `{ customerId, subject, description, categoryId, priority }` (needs `tickets.manage`) → **201** + `Location: /api/tickets/{id}` + the ticket with `number: "TKT-000001"`, `status: "new"`, `channel: "manual"`, the customer / category names and `createdAt` in UTC (AC 1, AC 4).
2. Missing `customerId` or blank `subject` → **400** with `errors.customerId` / `errors.subject` (AC 2). Unknown / soft-deleted customer → 400 `errors.customerId`; unknown or inactive category → 400 `errors.categoryId`; priority other than `high`/`mid`/`low` → 400 `errors.priority`. Description, category and priority are optional (priority defaults to **mid**, so every ticket has one — CRM-12 AC 4).
3. Ticket numbers are **unique and sequential**: the next number is `MAX(Number) + 1`, a **unique index** on `Number` rejects a duplicate taken by a concurrent request and the repository retries (AC 3).
4. `GET /api/tickets/{id}` (needs `tickets.view`) returns the ticket — also when its customer was soft-deleted afterwards (CRM-8 AC 5). CRM-15 turns it into the details page.
5. Client: the Tickets sidebar area shows a real page with **"New ticket"** (behind `<Can ticketsManage>`) opening a dialog: customer search + select, subject, description, category (active only, optional), priority (High / Mid / Low, default Mid). Success → toast "Ticket TKT-000001 was created.". The list itself arrives with CRM-14.

**Decisions — the `Ticket` entity (built to be extended by CRM-15..26)**

- `Crm.Domain/Tickets/Ticket.cs`: `Id` (Guid), `Number` (int, assigned on save), `Subject` (max 200), `Description` (nullable, max 10 000), `Status` (`TicketStatus { New, Open, Pending, Resolved, Closed }` — CRM-17 adds transitions), `Priority` (`TicketPriority`), `Channel` (`TicketChannel { Manual, Email, WhatsApp, Portal }` — CRM-23..26 create tickets with their channel), `CustomerId`, `CategoryId?`, `AssigneeId?` (CRM-16), `CreatedById?` (null for channel tickets), `CreatedAt`, `UpdatedAt` (UTC). Enums stored as strings (`HasConversion<string>()`, max 16); API names lower case (`TicketValues`).
- `Ticket.FormatNumber(int)` → `"TKT-000001"` (`D6`, more digits after 999 999); `Ticket.TryParseNumber("tkt-12" / "000012" / "12")` for the CRM-14 search.
- **Number generation without a database sequence** (SQLite in the tests has none): `TicketRepository.AddAsync` reads `MAX(Number)`, assigns `+1`, saves; on `DbUpdateException` (unique index `IX_Tickets_Number`) it retries (max 5). No gaps, works on both providers. `Ticket.AssignNumber(int)` only while unsaved by the repository.
- Tickets are **not** soft-deletable (they are closed, never deleted) → no query filter on `Ticket`.
- FKs without navigation properties (Domain stays free of Identity): `HasOne<Customer>()`, `HasOne<TicketCategory>()`, `HasOne<ApplicationUser>()` ×2 (assignee, creator), all `Restrict`. Reads use a `TicketView` projection (ticket + customer name ignoring the soft-delete filter + category name + assignee name).
- EF warns that `Customer` has a query filter and is the required end of the `Ticket` relationship; this is intended (handled by `IgnoreQueryFilters` in the ticket queries) → `CrmDbContext.OnConfiguring` ignores `CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`.
- Customer existence is checked by `ITicketRepository.CustomerExistsAsync` (soft-delete filter applies → deleted customer = not found, 400 like a missing one, since it is a field of the form).

**Not in scope:** list & filters (CRM-14), details page / replies / `FirstResponseAt` (CRM-15), assign (CRM-16), status changes (CRM-17), history (CRM-18), SLA due dates (CRM-19..22), channels (CRM-23..26), the timeline entry (after CRM-10).

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules + Architecture decisions; Frontend rules.
2. `.squad/stories/04-ticket-management/CRM-13/intake.md` — AC 1–4.
3. CRM-12 code: `server/src/Crm.Domain/Tickets/TicketCategory.cs`, `TicketPriority.cs`; `server/src/Crm.Application/Tickets/TicketValues.cs`, `TicketCategoryService.cs` (duplicate → `ValidationException` with a field dictionary), `ITicketCategoryRepository.cs` (`FindAsync`); `server/tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs` (`TestClock`, `FakeTicketCategoryRepository` — extend this file).
4. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 18–20 (`DbSet`s; add `Tickets` after line 20) and lines 22–45 (`OnModelCreating`, `ConfigureConventions`).
5. `server/src/Crm.Infrastructure/Customers/CustomerRepository.cs` (repository style), `server/src/Crm.Infrastructure/Identity/ApplicationUser.cs` (`FullName`).
6. `server/src/Crm.Application/Common/Security/ICurrentUser.cs` (`UserId`), `server/src/Crm.Api/Auth/AuthenticationExtensions.cs` line 49 (`ICurrentUser` scoped).
7. `server/src/Crm.Api/Endpoints/TicketCategoriesEndpoints.cs` (CRM-12 endpoint style) and `server/src/Crm.Api/Program.cs` line 35 (`app.MapTicketCategoriesEndpoints();` — map tickets after it).
8. `server/tests/Crm.Api.IntegrationTests/Customers/CustomerBodies.cs` (bodies), `Tickets/TicketBodies.cs` (extend), `Tickets/TicketCategoriesAuthorizationTests.cs` (policy dictionary pattern), `Infrastructure/CrmApiFactory.cs` lines 38 (`Time`), 95–100.
9. Client: `client/src/pages/ticket-categories/TicketCategoriesPage.tsx` + test (page pattern), `client/src/features/ticket-categories/TicketCategoryFormDialog.tsx` (dialog + server field errors), `client/src/features/customers/AddContactForm.tsx` lines 56–70 (`NativeSelect` in a `Controller`), `client/src/api/customers.ts` (`listCustomers`), `client/src/features/tickets/ticket-values.ts`, `client/src/app/AppRoutes.tsx` lines 27–29 (tickets route → `ComingSoonPage`), `client/src/App.layout.test.tsx` line 9 (`COMING_SOON_LABELS`).

---

## Backend Tasks

All commands from `server/`.

### 1 — Unit tests first (Red)

- **Create `server/tests/Crm.UnitTests/Tickets/TicketTests.cs`** — `Create_StartsNew_Manual_WithTrimmedSubject_AndUtcTimes`, `Create_WithBlankSubject_Throws`, `Create_WithEmptyCustomerId_Throws`, `Create_WithNonUtcTime_Throws`, `FormatNumber_PadsToSixDigits` (1 → `TKT-000001`, 1234567 → `TKT-1234567`), `TryParseNumber_AcceptsPrefixedAndPlainNumbers` (`"TKT-000012"`, `"tkt-12"`, `"12"` → 12; `"abc"`, `"TKT-"`, `"0"`, `""` → false), `AssignNumber_RejectsZeroOrNegative`.
- **Extend `TicketValuesTests.cs`** — status names `new, open, pending, resolved, closed`; channel names `manual, email, whatsapp, portal`; `TryParseStatus`.
- **Create `server/tests/Crm.UnitTests/Tickets/TicketServiceTests.cs`** (doubles in `TicketTestDoubles.cs`: `FakeTicketRepository` with `Customers` set and auto-numbering like the real repository, `FakeCurrentUser`) —
  - `Create_WithAllFields_ReturnsNumberStatusNewAndUtcCreatedAt` (AC 1, AC 4),
  - `Create_WithoutCustomerOrSubject_ThrowsValidationException` (AC 2: keys `customerId`, `subject`),
  - `Create_ForAnUnknownCustomer_ThrowsValidationException_OnCustomerId`,
  - `Create_WithAnInactiveOrUnknownCategory_ThrowsValidationException_OnCategoryId`,
  - `Create_WithAnInvalidPriority_ThrowsValidationException_OnPriority`,
  - `Create_WithoutPriority_UsesMid`, `Create_RecordsTheSignedInUserAsCreator`,
  - `Create_TwoTickets_GetConsecutiveNumbers` (AC 3),
  - `Get_UnknownTicket_ThrowsNotFound`.

Run `dotnet test tests/Crm.UnitTests` → Red (missing types).

### 2 — Domain + Application (Green)

- **Create `server/src/Crm.Domain/Tickets/TicketStatus.cs`**, **`TicketChannel.cs`**, **`Ticket.cs`** (see Decisions; `Ticket.Create(Guid customerId, string subject, string? description, Guid? categoryId, TicketPriority priority, TicketChannel channel, Guid? createdById, DateTime utcNow)`; `AssignNumber(int)`; `DisplayNumber`; `FormatNumber`; `TryParseNumber`).
- **File `server/src/Crm.Application/Tickets/TicketValues.cs`** — add status / channel name maps, `StatusNames`, `TryParseStatus`, `StatusName`, `ChannelName`.
- **Create `server/src/Crm.Application/Tickets/TicketContracts.cs`**:

```csharp
public sealed record CreateTicketRequest(Guid? CustomerId, string? Subject, string? Description, Guid? CategoryId, string? Priority);

public sealed record TicketResponse(
    Guid Id, string Number, string Subject, string? Description, string Status, string Priority, string Channel,
    Guid CustomerId, string CustomerName, Guid? CategoryId, string? CategoryName,
    Guid? AssigneeId, string? AssigneeName, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>A ticket with the names it shows (read model filled by the repository).</summary>
public sealed record TicketView(Ticket Ticket, string CustomerName, string? CategoryName, string? AssigneeName);
```

- **Create `CreateTicketRequestValidator.cs`** — `CustomerId` `NotEmpty()`; `Subject` `NotEmpty().MaximumLength(Ticket.SubjectMaxLength)`; `Description` `MaximumLength(Ticket.DescriptionMaxLength)`; `Priority` `.Must(p => TicketValues.TryParsePriority(p, out _)).WithMessage(_ => TicketText.PriorityInvalid).When(p is not blank)`; every rule `.WithName(_ => TicketText.<Field>)`.
- **Create `TicketText.cs`** — `CustomerField`, `SubjectField`, `DescriptionField`, `CategoryField`, `PriorityField`, `PriorityInvalid`, `CustomerNotFound`, `CategoryUnavailable`, `NotFound` (English + Arabic).
- **Create `ITicketRepository.cs`** — `Task<bool> CustomerExistsAsync(Guid customerId, ct)`; `Task AddAsync(Ticket ticket, ct)` (assigns the next number **and saves**); `Task<TicketView?> GetViewAsync(Guid id, ct)`.
- **Create `ITicketService.cs` / `TicketService.cs`** — ctor `(ITicketRepository tickets, ITicketCategoryRepository categories, ICurrentUser currentUser, TimeProvider timeProvider, IValidator<CreateTicketRequest> validator)`; `CreateAsync` (validate → customer exists → category active → `Ticket.Create(…, TicketChannel.Manual, currentUser.UserId, now)` → `AddAsync` → `GetViewAsync` → response); `GetAsync`. Field errors thrown as `ValidationException(new Dictionary<string, string[]> { ["customerId"] = […] })`.
- **File `server/src/Crm.Application/DependencyInjection.cs`** — register `ITicketService`.

### 3 — Integration tests (Red)

- **Create `server/tests/Crm.Api.IntegrationTests/Tickets/TicketCreationTests.cs`** (bodies `TicketBody` in `TicketBodies.cs`):
  - `CreateTicket_WithAllFields_Returns201_WithNumberAndStatusNew` (AC 1: `Location`, `number` matches `^TKT-\d{6}$`, `status == "new"`, `priority == "high"`, `channel == "manual"`, customer and category names),
  - `CreateTicket_StoresCreatedAtInUtc` (AC 4: the factory clock `factory.Time` is used; `createdAt.Kind == Utc` and equals the clock time; the row read through `CrmDbContext` has `Kind == Utc`),
  - `CreateTicket_WithoutCustomerOrSubject_Returns400` (AC 2: `errors` contains `customerId` and `subject`),
  - `CreateTicket_ForADeletedCustomer_Returns400`, `CreateTicket_WithAnInactiveCategory_Returns400` (CRM-12 AC 3), `CreateTicket_WithAnUnknownPriority_Returns400`,
  - `TicketNumbers_AreUniqueAndSequential` (AC 3: three tickets → numbers n, n+1, n+2),
  - `GetTicket_OfASoftDeletedCustomer_StillShowsTheCustomer` (CRM-8 AC 5),
  - `GetTicket_KeepsADeactivatedCategoryName` (CRM-12 AC 3 "stays on old tickets"),
  - `GetTicket_Unknown_Returns404`.
- **Create `Tickets/TicketsAuthorizationTests.cs`** — 401 without token (POST, GET by id); 403 for a user without roles; `TicketEndpoints_NeedViewToRead_AndManageToCreate` (policy dictionary: `"POST /api/tickets/"` = manage + view, `"GET /api/tickets/{id:guid}"` = view).

### 4 — Infrastructure + Api (Green)

- **Create `server/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`** — table `Tickets`; `Number` unique index; `Subject` max 200 required; `Description` max 10 000; enums `HasConversion<string>().HasMaxLength(16)`; FKs `Restrict` (customer required, category / assignee / creator optional); index `CreatedAt` (CRM-14 default order).
- **File `CrmDbContext.cs`** — `public DbSet<Ticket> Tickets => Set<Ticket>();` after line 20; `OnConfiguring` ignores `CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` (comment: ticket queries read customers with `IgnoreQueryFilters`).
- **Create `server/src/Crm.Infrastructure/Tickets/TicketRepository.cs`** — `CustomerExistsAsync` (`db.Customers.AnyAsync`, filter on), `AddAsync` (max + 1, unique-index retry), `GetViewAsync` (projection: `join db.Customers.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])`, left joins `TicketCategories` and `Users`). Register in `Crm.Infrastructure/DependencyInjection.cs`.
- **Create `server/src/Crm.Api/Endpoints/TicketsEndpoints.cs`** — group `/api/tickets` `.RequireAuthorization(Permissions.TicketsView)`; `POST ""` `.RequireAuthorization(Permissions.TicketsManage)` → `Results.Created($"/api/tickets/{id}", ticket)`; `GET "/{id:guid}"`. **`Program.cs`**: `app.MapTicketsEndpoints();`.

### 5 — Migration

`dotnet ef migrations add AddTickets --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations` → `CreateTable("Tickets")` with 4 FKs + indexes only.

---

## Frontend Tasks

All commands from `client/`.

### 1 — Tests first (Red)

- **Create `client/src/api/tickets.test.ts`** — `createTicket` → `POST /api/tickets` with the body; `getTicket('t1')` → `GET /api/tickets/t1`.
- **Extend `client/src/features/tickets/ticket-values.test.ts`** — `ticketStatuses` / `ticketChannels` names with en + ar labels.
- **Create `client/src/pages/tickets/TicketsPage.test.tsx`** (mocks `@/api/auth`, `@/api/tickets`, `@/api/customers`, `@/api/ticket-categories`):
  - creates a ticket: "New ticket" → dialog "New ticket"; customers listed in the "Customer" select; category select offers **only** the active categories (`listTicketCategories({ activeOnly: true })`, CRM-12 AC 1/3); priority select offers High / Mid / Low with Mid preselected; submit → `createTicket({ customerId, subject, description, categoryId, priority: 'high' })`; toast "Ticket TKT-000001 was created." (AC 1);
  - searching customers calls `listCustomers({ search: 'nour', pageSize: 20 })`;
  - requires customer and subject before calling the API (AC 2);
  - shows server field errors (400 `errors.customerId`) under the fields;
  - hides "New ticket" without `tickets.manage`.
- **File `client/src/App.layout.test.tsx`** line 9 — remove `'Tickets'` from `COMING_SOON_LABELS`; add `opens the tickets page from the sidebar` (heading "Tickets").

### 2 — Implementation (Green)

- `npx shadcn@latest add textarea`.
- **Create `client/src/api/tickets.ts`** — `Ticket`, `CreateTicketRequest`, `createTicket`, `getTicket`.
- **File `client/src/features/tickets/ticket-values.ts`** — `ticketStatuses`, `ticketChannels`.
- **Create `client/src/features/tickets/`** — `ticket-form-schema.ts` (zod: `customerId` required, `subject` 1–200, `description` ≤ 10 000, `categoryId` optional, `priority` enum), `NewTicketDialog.tsx`, `useTickets.ts` (`ticketsQueryKey = ['tickets']`; the dialog invalidates it for CRM-14).
- **Create `client/src/pages/tickets/TicketsPage.tsx`** — heading `nav.tickets`, description, "New ticket" behind `<Can permission={permissions.ticketsManage}>`.
- **File `client/src/app/AppRoutes.tsx`** — tickets route element → `<TicketsPage />` (inside the existing `RequirePermission ticketsView`).
- **i18n** — `tickets.*`: description, new, createTitle, createDescription, customer, customerSearch, customerFind, customerPlaceholder, subject, description field, category, noCategory, priority, save, saving, cancel, customerRequired, subjectRequired, created ("Ticket {{number}} was created."), `statuses.*`, `channels.*`.

---

## Follow-up after CRM-10 merges: record a TicketCreated timeline entry

CRM-10 (other branch) adds `Crm.Application/Customers/Timeline/IInteractionRecorder` (`Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow)`, called **before** the feature's own `SaveChangesAsync` so the entry is saved in the same unit of work), `Crm.Domain/Customers/InteractionType.Ticket` and `InteractionEvents` (event codes, client labels `customers.timeline.events.<code>`). When both branches are on `main`:

1. Add `public const string TicketCreated = "ticketCreated";` to `Crm.Domain/Customers/InteractionEvents.cs`, and `customers.timeline.events.ticketCreated` ("Ticket created" / "تم إنشاء تذكرة") to `client/src/i18n/{en,ar}.json`.
2. Inject `IInteractionRecorder` into `TicketService` and call `recorder.Record(ticket.CustomerId, InteractionType.Ticket, InteractionEvents.TicketCreated, ticket.Subject, ticket.Id, now)` **before** `tickets.AddAsync(ticket, …)` (that call saves; the entry joins the same save and is retried with the ticket when the number collides).
3. Tests first: `TicketServiceTests.Create_RecordsATicketCreatedTimelineEntry` (fake recorder) and an integration test that `GET /api/customers/{id}/timeline?type=ticket` lists the new ticket.

---

## Edge Cases & Failure Modes

- **Two agents create a ticket at the same moment** → both read the same `MAX(Number)`; the unique index `IX_Tickets_Number` rejects the second insert; `TicketRepository.AddAsync` retries with a fresh max (5 attempts, then the exception → 500). Not reproducible with SQLite's single connection; covered by the design + unique index.
- **Customer deleted between picking and saving** → 400 `errors.customerId` ("The customer was not found.").
- **Customer deleted after the ticket was created** → ticket still returned with the customer name (`IgnoreQueryFilters` in `GetViewAsync`).
- **Category deactivated between loading the form and saving** → 400 `errors.categoryId`.
- **Priority omitted** → `mid`; **priority "urgent" / "1"** → 400.
- **Empty GUID `customerId`** → 400 (`NotEmpty`).
- **Very long subject / description** → 400 (validator) before the database.
- **Creator** = `ICurrentUser.UserId` (null only for channel tickets later).
- **Numbers above 999 999** → `TKT-1000000` (format pads to at least 6 digits).

---

## Test Plan

1. Unit: `Crm.UnitTests/Tickets/TicketTests.cs`, `TicketServiceTests.cs`, `TicketValuesTests.cs` (extended); doubles in `TicketTestDoubles.cs`.
2. Integration: `Crm.Api.IntegrationTests/Tickets/TicketCreationTests.cs` (AC 1–4 + soft-deleted customer + inactive category), `TicketsAuthorizationTests.cs`; `PermissionPolicyTests` and `SoftDeleteModelTests` unchanged and green.
3. Client: `api/tickets.test.ts`, `features/tickets/ticket-values.test.ts`, `pages/tickets/TicketsPage.test.tsx`, `App.layout.test.tsx`.

---

## Migration / Rollback

- `AddTickets` only creates `Tickets` (FKs to `Customers`, `TicketCategories`, `AspNetUsers` with `Restrict`). Rollback: `dotnet ef database update AddTicketCategories` drops the table (ticket data lost — only before go-live).
- After CRM-10/11 merge, both branches add migrations after `AddCustomerContacts`; the merger keeps both migration files and resolves `CrmDbContextModelSnapshot.cs` so it contains every entity (then `dotnet ef migrations add CheckModel` must produce an empty migration, which is deleted).

---

## Verification Steps

1. **Backend builds:** `cd server && dotnet build` → 0 warnings / 0 errors.
2. **Backend tests:** `cd server && dotnet test` → green.
3. **Frontend:** `cd client && npm test && npm run build && npm run lint` → green.
4. **Regression:** `PermissionPolicyTests` (new endpoints have permissions; SuperAdmin `{}` POST → 400, never 401/403).

---

## Done Criteria

- [ ] AC 1: POST with all fields → 201, `TKT-` number, status `new`.
- [ ] AC 2: missing customer or subject → 400 with field errors.
- [ ] AC 3: consecutive numbers, unique index on `Number`.
- [ ] AC 4: `CreatedAt` UTC (clock time, `Kind == Utc` in API and database).
- [ ] Ticket of a soft-deleted customer still returned with its customer; deactivated category name kept.
- [ ] "New ticket" dialog (active categories only, High/Mid/Low) on the Tickets page; strings in en + ar.
- [ ] All builds/tests/lint green.

---

## How later stories build on this (write nothing here; for later planners)

- **CRM-14 (list):** add `ITicketRepository.ListAsync(TicketListFilter, page, pageSize)` reusing the `TicketView` projection of `TicketRepository.GetViewAsync` (customers via `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])`); search by number with `Ticket.TryParseNumber`; newest first = `OrderByDescending(CreatedAt).ThenByDescending(Number)` (index on `CreatedAt`). Invalidate `ticketsQueryKey` after creation (already done by the dialog).
- **CRM-15 (details & replies):** `GET /api/tickets/{id}` exists; add `FirstResponseAt` (nullable UTC) to `Ticket`, messages as a child entity with `TicketId` FK. Route `tickets/:id` inside the same `RequirePermission`.
- **CRM-16 (assign):** set `Ticket.AssigneeId` through a Domain method; FK to `AspNetUsers` already exists; `TicketView.AssigneeName` already projected.
- **CRM-17 (status workflow):** transitions on `Ticket` (`New → Open → Pending → Resolved → Closed`, reopen) as Domain methods; `TicketValues.TryParseStatus` exists.
- **CRM-19..22 (SLA):** due times per `TicketPriority` from `CreatedAt`.
- **CRM-23..26 (channels):** `Ticket.Create(…, TicketChannel.Email / WhatsApp / Portal, createdById: null, …)` through `ITicketRepository.AddAsync` (numbers stay sequential across channels).
