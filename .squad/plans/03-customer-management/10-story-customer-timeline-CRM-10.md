# Story 10 — Customer interaction history (Story: CRM-10)

## Prerequisites

- Stories 01–09 completed and merged to `main` (foundation, security-admin, [08-story-customer-profiles-CRM-8.md](08-story-customer-profiles-CRM-8.md), [09-story-customer-contacts-CRM-9.md](09-story-customer-contacts-CRM-9.md)). Binding: CRM-8 "6 — How later stories build on this" lines 1576–1581 (child entities with `CustomerId` FK `Restrict`, own `IEntityTypeConfiguration<T>`, routes under `/api/customers/{id:guid}/…`, parent checked with `ICustomerRepository.FindAsync` → 404 for a deleted customer, a details page at `customers/:id`, UTC `DateTime` passed in, one migration per story) and CRM-9 "6 — How later stories build on this" (`useCustomer(id)` exists; `Customer.AddContact` is the place to raise "contact added").
- Permissions from the CRM-7 catalogue ([../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md) line 46): the timeline is a read → `customers.view`.
- Branch **`feature/group-a-customers`** (CRM-10 and CRM-11 together; one or more commits per story).
- No new NuGet / npm package, no new shadcn component (`native-select`, `table`, `badge`, `button` exist). One migration: **`AddCustomerInteractions`**.

---

## Story Goal

Agents open a customer and see everything that happened with that customer, newest first, so they have full context before replying.

1. `GET /api/customers/{id}/timeline?type=&page=&pageSize=` → **200** `PagedResult<CustomerInteractionResponse>` ordered **newest first** (AC 1, AC 4). `type` (optional) = `customer` | `note` | `attachment` | `ticket` | `message` filters the entries (AC 3); an unknown type → **400** `errors.type`; paging as everywhere (`page` ≥ 1, `pageSize` 1–100, default 20).
2. What exists today is recorded now: **customer created**, **customer updated**, **contact added** (type `customer`), each with the acting user and the UTC time. CRM-11 adds `noteAdded` / `attachmentAdded`; tickets and messages plug in later through the same recorder.
3. **AC 2 ("creating a ticket adds an entry") is completed by CRM-13**: this story provides the mechanism (`IInteractionRecorder`, `InteractionType.Ticket`) and a test that an entry recorded through it shows up in the timeline; **CRM-13 must call the recorder when it creates a ticket and add the test `CreateTicket_AddsATimelineEntry`**.
4. Client: a **customer details page** `customers/:id` (name, contacts, timeline with a type filter and Previous / Next paging); the customer name in the customers table links to it. All text in English and Arabic.
5. Unknown / deleted customer → 404. No token → 401, without `customers.view` → 403.

**Decisions**

- **Write-side entry table, not a query-side union.** A `CustomerInteractions` table (Domain `CustomerInteraction`) filled through an Application-level **`IInteractionRecorder`** by every feature that does something with a customer. Why: (a) the union would have to know every source table (tickets, messages, notes, attachments, contact changes) and grow with each story, and paging + ordering across a SQL `UNION` of heterogeneous tables is expensive and awkward in EF Core; (b) events without their own table (customer created / updated, contact added — contacts are hard-deleted) could not appear at all; (c) a single indexed table `(CustomerId, OccurredAt)` pages cheaply. Cost: one extra row per event and the rule "the feature must call the recorder" — documented in "How later stories build on this".
- **Same unit of work.** `IInteractionRecorder.Record(...)` only **adds** the entry to the scoped `CrmDbContext` (through `ICustomerTimelineRepository.Add`); the caller's next `SaveChangesAsync` commits the business change and its timeline entry together (no entry without the change, no change without the entry).
- **Entry shape:** `Type` (filter category, stored by name), `Event` (camelCase code such as `customerCreated`, translated by the client — no language-specific text stored), `Details` (short free text: the name, the contact value, a note excerpt; max 500, longer is cut with "…"), `SourceId` (id of the ticket / message / note / attachment, nullable), `ActorId` (current user, null for system / channel events), `OccurredAt` (UTC from `TimeProvider`).
- **`long` identity key** (database-generated): entries written in one request share `OccurredAt`; ordering by `OccurredAt DESC, Id DESC` keeps insertion order deterministic on SQL Server and SQLite.
- **Actor name** is read with a left join to the Identity users table in the Infrastructure query (a renamed user shows the new name; a missing user shows nothing). The Application layer never sees Identity types.
- **Single `type` filter** (one value, or none = all) is enough for AC 3; a list of types can be added later without breaking the contract.
- **No navigation** `CustomerInteraction.Customer`: the FK is configured with `HasOne<Customer>().WithMany()`; without a navigation EF does not raise the "required navigation + query filter" warning, and the timeline service checks the customer with `ICustomerRepository.FindAsync` (deleted → 404).

**Not in scope:** tickets / messages (CRM-12..18, 23..26), notes and attachments (CRM-11), editing / deleting entries, real-time updates, a field-level audit log, customer deleted (a deleted customer has no visible timeline).

---

## Context — Read These Files First

1. `CLAUDE.md` lines 41–51 (backend rules), 63–80 (architecture decisions: Application service per feature, pagination, soft delete).
2. `.squad/stories/03-customer-management/CRM-10/intake.md` — AC 1–4, out of scope.
3. `server/src/Crm.Domain/Customers/Customer.cs` lines 49–55 (`Create`), 62–67 (`Update`), 92–111 (`AddContact`) — the events recorded.
4. `server/src/Crm.Application/Customers/CustomerService.cs` lines 10–17 (constructor — gets `IInteractionRecorder`), 37–46 (`CreateAsync`), 48–57 (`UpdateAsync`), 67–83 (`AddContactAsync`), 113 (`UtcNow`), 121–122 (`FindAsync`).
5. `server/src/Crm.Application/Customers/ICustomerRepository.cs` line 19 (`FindAsync`, filter-aware), `CustomerText.cs` (new texts at the end), `ListCustomersQueryValidator.cs` lines 10–11 (paging rules to copy), `Common/Paging/PagedResult.cs`, `Common/Security/ICurrentUser.cs` line 7 (`UserId`).
6. `server/src/Crm.Application/DependencyInjection.cs` lines 12–13, `server/src/Crm.Infrastructure/DependencyInjection.cs` line 40 (registrations).
7. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` line 17 (`DbSet` pattern), `Persistence/Configurations/CustomerConfiguration.cs` (configuration style), `Customers/CustomerRepository.cs` lines 33–39 (count + page).
8. `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs` lines 26–28 (`GetCustomer` — the timeline route goes after it).
9. Tests: `server/tests/Crm.UnitTests/Customers/CustomerServiceTests.cs` lines 14–18 (constructor), 211–247 (fake repository), 250+ (`TestClock`); `server/tests/Crm.Api.IntegrationTests/Customers/CustomersAuthorizationTests.cs` lines 14–25 (`Endpoints()`), 72–95 (policy map, count 9); `Infrastructure/CrmApiFactory.cs` lines 38 (`Time`), 82–92 (`CreateUserAsync`: `FullName` = email); `Customers/CustomerContactsTests.cs` lines 13–27 (helpers).
10. Client: `client/src/api/customers.ts` lines 44–51, `client/src/api/paging.ts`, `client/src/features/customers/useCustomers.ts` (key prefix `['customers']`), `CustomersTable.tsx` line 31 (name cell → link), `CustomerContactsTable.tsx` (reused on the details page), `client/src/app/AppRoutes.tsx` lines 27–29, `client/src/components/ui/native-select.tsx`, `client/src/pages/customers/CustomerContacts.test.tsx` lines 1–70 (mock pattern), `client/src/no-hardcoded-text.test.ts` lines 52–61 (English values > 3 chars never quoted in code, comments included).

---

## Backend Tasks

All commands from `server/`.

### 1 — Unit tests first (Red)

- **Create** `server/tests/Crm.UnitTests/Customers/CustomerInteractionTests.cs` (Domain): `Create_SetsEveryField`, `Create_TrimsDetails_AndEmptyBecomesNull`, `Create_CutsLongDetails_To500Characters`, `Create_WithoutEvent_Throws`, `Create_WithEmptyCustomerId_Throws`, `Create_WithNonUtcTime_Throws`.
- **Create** `.../InteractionRecorderTests.cs` (Application): `Record_AddsAnEntry_WithTheCurrentUserAsActor`, `Record_WithoutSignedInUser_HasNoActor`.
- **Create** `.../CustomerTimelineServiceTests.cs`: `List_ReturnsTheRepositoryPage_WithDefaults`, `List_WithType_PassesTheParsedType`, `List_WithUnknownType_ThrowsValidationException_OnType`, `List_WithInvalidPaging_ThrowsValidationException`, `List_OfUnknownCustomer_ThrowsNotFound`.
- **Modify** `.../CustomerServiceTests.cs`: constructor passes a `FakeInteractionRecorder` (records the calls); new `Create_RecordsCustomerCreated`, `Update_RecordsCustomerUpdated`, `AddContact_RecordsContactAdded`, `InvalidCreate_RecordsNothing`.

Run `dotnet test tests/Crm.UnitTests` → compile errors (Red).

### 2 — Domain + Application (Green for unit tests)

- **Create** `server/src/Crm.Domain/Customers/InteractionType.cs`: `enum InteractionType { Customer, Note, Attachment, Ticket, Message }`.
- **Create** `server/src/Crm.Domain/Customers/InteractionEvents.cs`: constants `CustomerCreated = "customerCreated"`, `CustomerUpdated = "customerUpdated"`, `ContactAdded = "contactAdded"` (later stories add theirs).
- **Create** `server/src/Crm.Domain/Customers/CustomerInteraction.cs`: private setters; `long Id`, `Guid CustomerId`, `InteractionType Type`, `string Event` (max 64), `string? Details` (max 500), `Guid? SourceId`, `Guid? ActorId`, `DateTime OccurredAt`; `static Create(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, Guid? actorId, DateTime utcNow)` with the rules of the tests.
- **Create** `server/src/Crm.Application/Customers/Timeline/`:
  - `InteractionTypes.cs` — `TryParse(string?, out InteractionType)` (lower-case names only, no numbers) and `Name(InteractionType)`.
  - `CustomerTimelineContracts.cs` — `CustomerTimelineQuery(string? Type, int? Page, int? PageSize)`; `CustomerInteractionResponse(long Id, string Type, string Event, string? Details, Guid? SourceId, Guid? ActorId, string? ActorName, DateTime OccurredAt)`.
  - `CustomerTimelineQueryValidator.cs` — type parses (`CustomerText.TimelineTypeInvalid`, name `CustomerText.TimelineTypeField`), paging rules as `ListCustomersQueryValidator`.
  - `ICustomerTimelineRepository.cs` — `void Add(CustomerInteraction)`; `Task<PagedResult<CustomerInteractionResponse>> ListAsync(Guid customerId, InteractionType? type, int page, int pageSize, CancellationToken)` (newest first).
  - `IInteractionRecorder.cs` + `InteractionRecorder.cs` — `void Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow)`; builds the entry with `ActorId = currentUser.UserId` and adds it (saved by the caller's `SaveChangesAsync`).
  - `ICustomerTimelineService.cs` + `CustomerTimelineService.cs` — validate, `customers.FindAsync` (→ `NotFoundException(CustomerText.NotFound)`), repository page.
- **Modify** `CustomerText.cs`: `TimelineTypeField` ("Type" / "النوع"), `TimelineTypeInvalid` ("Choose customer, note, attachment, ticket or message." / Arabic).
- **Modify** `CustomerService.cs`: inject `IInteractionRecorder timeline`; after the Domain call in `CreateAsync` / `UpdateAsync` record `Customer` + `CustomerCreated` / `CustomerUpdated` (details = name), in `AddContactAsync` record `ContactAdded` (details = stored value, sourceId = contact id). Same `utcNow` as the change.
- **Modify** `Crm.Application/DependencyInjection.cs`: `AddScoped<IInteractionRecorder, InteractionRecorder>()`, `AddScoped<ICustomerTimelineService, CustomerTimelineService>()`.

### 3 — Integration tests (Red)

- **Create** `server/tests/Crm.Api.IntegrationTests/Customers/CustomerTimelineTests.cs`: `Timeline_ShowsWhatHappened_NewestFirst_WithActorAndTime` (AC 1: create → +1 min add contact → +1 min update; events `customerUpdated, contactAdded, customerCreated`, actor name, UTC times), `Timeline_FilteredByType_ReturnsOnlyThatType` ×3 (`note`, `ticket`, `message` — entries of those types written through `IInteractionRecorder` in a test scope; AC 3), `Timeline_IsPaginated` (25 entries, `pageSize=10` → 10/10/5, `totalCount` 25; AC 4), `RecordedTicketEntry_ShowsInTheTimeline` (mechanism for AC 2), `Timeline_WithUnknownType_Returns400` , `Timeline_WithInvalidPageSize_Returns400`, `Timeline_OfUnknownOrDeletedCustomer_Returns404`.
- **Modify** `CustomersAuthorizationTests.cs`: row `{ "GET", $"/api/customers/{Guid.Empty}/timeline" }`; policy `GET /api/customers/{id:guid}/timeline` = read; count 10.

### 4 — Infrastructure + Api (Green)

- **Create** `server/src/Crm.Infrastructure/Persistence/Configurations/CustomerInteractionConfiguration.cs`: table `CustomerInteractions`; key `Id` `ValueGeneratedOnAdd`; `Type` string (16); `Event` 64 required; `Details` 500; `HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict)`; index `(CustomerId, OccurredAt)`.
- **Modify** `CrmDbContext.cs`: `public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();`.
- **Create** `server/src/Crm.Infrastructure/Customers/CustomerTimelineRepository.cs`: `Add` → `db.CustomerInteractions.Add`; `ListAsync` filters customer (+ type), counts, orders `OccurredAt DESC, Id DESC`, pages, left-joins `db.Users` for `FullName`, maps `Type` with `InteractionTypes.Name`.
- **Modify** `Crm.Infrastructure/DependencyInjection.cs`: `AddScoped<ICustomerTimelineRepository, CustomerTimelineRepository>()`.
- **Modify** `CustomersEndpoints.cs`: `group.MapGet("/{id:guid}/timeline", ([AsParameters] CustomerTimelineQuery query, …) => Results.Ok(await timeline.ListAsync(id, query, ct))).WithName("GetCustomerTimeline")` (group policy `customers.view`).
- **Migration:** `dotnet ef migrations add AddCustomerInteractions --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations` → creates only `CustomerInteractions` + FK + index. Not hand-edited. Existing customers get no back-filled entries (history starts with this release).

### 5 — How later stories build on this (write nothing here; for later planners)

- **Any feature that does something with a customer** injects `IInteractionRecorder` and calls `Record(customerId, InteractionType.X, InteractionEvents.Y, details, sourceId, utcNow)` **before** its `SaveChangesAsync` (same scoped `CrmDbContext` → one transaction). Add the event constant to `InteractionEvents` and its label under `customers.timeline.events.<event>` in `client/src/i18n/{en,ar}.json` (+ the `knownEvents` list in `client/src/features/customers/CustomerTimeline.tsx`).
- **CRM-11 (notes, attachments):** `InteractionType.Note` + `noteAdded` (details = note excerpt, sourceId = note id), `InteractionType.Attachment` + `attachmentAdded` (details = file name, sourceId = attachment id).
- **CRM-13 (create ticket) completes CRM-10 AC 2:** record `InteractionType.Ticket` + `ticketCreated` (details = ticket number + subject, sourceId = ticket id) in the ticket create use case and add `CreateTicket_AddsATimelineEntry` (integration: create a ticket, `GET /api/customers/{id}/timeline?type=ticket` returns it). Status changes may record `ticketStatusChanged`.
- **CRM-15 / CRM-23..26 (messages):** `InteractionType.Message` + `messageReceived` / `messageSent` (details = first characters, sourceId = message id). Channel webhooks are anonymous → `ActorId` null (shown as "System").
- **Client links:** `SourceId` lets the timeline link to `/tickets/{sourceId}` once ticket pages exist (CRM-14).

---

## Frontend Tasks

All commands from `client/`. No new package or shadcn component.

### 1 — Tests first (Red)

- **Modify** `client/src/api/customers.test.ts`: `getCustomerTimeline` builds `/api/customers/c1/timeline?type=note&page=2&pageSize=10` (and omits empty params).
- **Create** `client/src/pages/customers/CustomerDetailsPage.test.tsx` (mocks `@/api/auth`, `@/api/customers`, renders the page in a `MemoryRouter` at `/customers/c1`): `shows the customer and the timeline newest first` (AC 1: entries in API order with translated event, details, actor), `filters the timeline by type` (AC 3: select "Notes" → called with `type: 'note'`, page 1), `pages through the timeline` (AC 4: Next → page 2, Previous disabled on page 1), `shows a system entry without an actor`, `shows not found for an unknown customer`.
- **Modify** `client/src/pages/customers/CustomersPage.test.tsx`: `links each customer name to its details page`.

### 2 — Implementation (Green)

- **Modify** `client/src/api/customers.ts`: types `InteractionType`, `CustomerInteraction`, `TimelineParams` (`type?`, `page?`, `pageSize?`); `getCustomerTimeline(id, params, signal)`.
- **Create** `client/src/features/customers/useCustomerTimeline.ts`: key `[...customersQueryKey, 'timeline', id, params]`, `keepPreviousData`.
- **Create** `client/src/features/customers/CustomerTimeline.tsx`: filter `NativeSelect` (All, Customer changes, Notes, Attachments, Tickets, Messages), ordered list (`<ol>`) of entries — event label `t('customers.timeline.events.<event>')` (unknown → "Other activity"), details `dir="auto"`, actor or "System", time via `Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })`; "Page x of y" + Previous / Next; empty and loading states. Page size 10; changing the filter resets to page 1.
- **Create** `client/src/pages/customers/CustomerDetailsPage.tsx`: `useParams().id`, `useCustomer(id)`; back link to `/customers`; heading = name; contacts (`CustomerContactsTable`, or "No contacts yet."); `<CustomerTimeline customerId={id} />`; 404 → "Customer not found." message.
- **Modify** `client/src/app/AppRoutes.tsx`: `<Route path="customers/:id" element={<CustomerDetailsPage />} />` inside the `customersView` `RequirePermission`.
- **Modify** `CustomersTable.tsx`: name cell → `<Link to={`/customers/${customer.id}`}>` (`text-primary underline-offset-4 hover:underline`).
- **Modify** `en.json` / `ar.json`: `customers.details.*` (back, contacts, notFound, loading), `customers.timeline.*` (title, filter label, filter options, events.customerCreated / customerUpdated / contactAdded / other, system, empty, loading, pageInfo, previous, next).

---

## Edge Cases & Failure Modes

- **Same timestamp** (create with a phone records nothing extra; create + quick update) → `OccurredAt DESC, Id DESC` (identity) keeps insertion order (`CustomerTimelineRepository.ListAsync`).
- **Unknown type** (`type=foo`, `type=1`, `type=Note`) → 400 `errors.type` (`CustomerTimelineQueryValidator`; `InteractionTypes.TryParse` accepts only lower-case names).
- **Paging out of range** → `page=0` / `pageSize=0|101` → 400; a page after the last → 200 with empty `items` and the real `totalCount`.
- **Unknown / soft-deleted customer** → 404 `CustomerText.NotFound` (`CustomerTimelineService` → `ICustomerRepository.FindAsync`, filtered).
- **Business change fails validation** → nothing recorded (recording happens after validation and the Domain call; no `SaveChanges` → nothing persisted).
- **Actor deleted / renamed** → left join: name null → client shows "System"; renamed → new name.
- **Long details** → cut to 500 characters with "…" (`CustomerInteraction.Create`).
- **Unknown future event code on an old client** → "Other activity" (client fallback).
- **Arabic** → labels translated; details `dir="auto"`, values like phone numbers stay readable.
- **Permissions** → timeline needs `customers.view` (401 / 403 rows in `CustomersAuthorizationTests`); `PermissionPolicyTests` picks up the route automatically.

---

## Test Plan

1. Unit (new): `CustomerInteractionTests` (6), `InteractionRecorderTests` (2), `CustomerTimelineServiceTests` (5).
2. Unit (modified): `CustomerServiceTests` (+4, constructor gets the fake recorder). `LocalizedTextCatalogTests` picks up the two new `CustomerText` properties.
3. Integration (new): `CustomerTimelineTests` (9 incl. theory rows).
4. Integration (modified): `CustomersAuthorizationTests` (+2 rows, policy map 10).
5. Unchanged guards: `PermissionPolicyTests`, `SoftDeleteModelTests`, `LayerDependencyTests`.
6. Client (new): `CustomerDetailsPage.test.tsx` (5); (modified) `customers.test.ts` (+2), `CustomersPage.test.tsx` (+1); guards `translations.test.ts`, `no-hardcoded-text.test.ts`, `theme.test.ts`.

**Deviations (as built):** the unit-test fakes shared by several test classes live in `server/tests/Crm.UnitTests/Customers/TimelineTestDoubles.cs` (`FakeInteractionRecorder`, `FakeTimelineRepository`, `FakeCurrentUser`); the client got one extra test (`says when nothing happened yet`, 6 in `CustomerDetailsPage.test.tsx`); because the customer name is now a `<Link>`, `renderPage` in `CustomersPage.test.tsx` and `CustomerContacts.test.tsx` wraps the page in a `MemoryRouter`. Results: `dotnet test` 241 unit + 185 integration, `npm test` 659 in 25 files, build and lint green.

---

## Migration / Rollback

- `AddCustomerInteractions`: table `CustomerInteractions` (`Id bigint IDENTITY PK`, `CustomerId` FK → `Customers.Id` **NO ACTION**, `Type nvarchar(16)`, `Event nvarchar(64)`, `Details nvarchar(500) NULL`, `SourceId`, `ActorId` nullable `uniqueidentifier`, `OccurredAt datetime2`), index `IX_CustomerInteractions_CustomerId_OccurredAt`. No data migration.
- Rollback: `dotnet ef database update AddCustomerContacts --project src/Crm.Infrastructure --startup-project src/Crm.Api` drops the table (timeline history lost; nothing else depends on it).

---

## Verification Steps

1. **Backend builds:** `server/`: `dotnet build` — 0 warnings, 0 errors.
2. **Backend tests:** `server/`: `dotnet test` — all green.
3. **Frontend:** `client/`: `npm test`, `npm run build`, `npm run lint` — all green.
4. **Migration check:** only `*_AddCustomerInteractions.cs`, its `.Designer.cs` and `CrmDbContextModelSnapshot.cs` added/changed under `Migrations/`.
5. **Regression:** `git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'` → only `client/src/api/client.ts`; no change under `client/src/components/ui/`.

---

## Done Criteria

- [ ] Customer page shows the timeline newest first (`Timeline_ShowsWhatHappened_NewestFirst_WithActorAndTime`; UI `shows the customer and the timeline newest first`) — AC 1.
- [ ] Recorder mechanism for ticket entries in place and tested (`RecordedTicketEntry_ShowsInTheTimeline`); **AC 2 completed by CRM-13** (`CreateTicket_AddsATimelineEntry`) — AC 2.
- [ ] Filter by type (`Timeline_FilteredByType_ReturnsOnlyThatType`; UI `filters the timeline by type`) — AC 3.
- [ ] Paginated (`Timeline_IsPaginated`; UI `pages through the timeline`) — AC 4.
- [ ] Read needs `customers.view`; texts in `CustomerText` and both i18n files.
- [ ] `dotnet build` / `dotnet test` / `npm test` / `npm run build` / `npm run lint` green; committed as `CRM-10: customer interaction timeline`.
