# Story 14 — Ticket list & filters (Story: CRM-14)

## Prerequisites

- Story 13 completed: [13-story-create-ticket-CRM-13.md](13-story-create-ticket-CRM-13.md) (CRM-13, same branch, incl. its "Deviations (as built)"). Its "How later stories build on this" is **binding**: `ITicketRepository` gets a list method reusing the ticket projection of `TicketRepository` (customers through `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])`); search by number with `Ticket.TryParseNumber`; newest first = `OrderByDescending(CreatedAt).ThenByDescending(Number)` (index `IX_Tickets_CreatedAt`); `ticketsQueryKey` is already invalidated by the new-ticket dialog.
- Story 12: [12-story-ticket-categories-CRM-12.md](12-story-ticket-categories-CRM-12.md) — the category filter uses `listTicketCategories({})` (inactive categories included, so old tickets can be found).
- [../02-security-admin/06-story-user-management-CRM-6.md](../02-security-admin/06-story-user-management-CRM-6.md) line 1382 (**binding**: paged lists reuse `PagedResult<T>` + `PagingDefaults`, a `List<Feature>Query` bound with `[AsParameters]`, the same page / pageSize validator rules, `EF.Functions.Like` with escaping) and [../03-customer-management/08-story-customer-profiles-CRM-8.md](../03-customer-management/08-story-customer-profiles-CRM-8.md) line 1579 (`LikePattern.Contains` + `LikePattern.EscapeCharacter`, `PagingText`).
- CRM-10 / CRM-11 are merged into this branch. No new packages, no new shadcn component, **no migration** (the `CreatedAt` index exists since `AddTickets`).

---

## Story Goal

Agents find tickets quickly on the Tickets page.

1. `GET /api/tickets` (needs `tickets.view`) → `PagedResult<TicketResponse>`, **newest first** (`CreatedAt` desc, then `Number` desc), `page` default 1, `pageSize` default 20 (max 100) (AC 3).
2. Filters (AC 1), all optional: `status` (`new|open|pending|resolved|closed`), `priority` (`high|mid|low`), `categoryId`, `assigneeId` **or** `unassigned=true`, `createdFrom` / `createdTo` (dates `yyyy-MM-dd`, inclusive, UTC days).
3. `search` (AC 2): a ticket number (`TKT-000012`, `tkt-12`, `12`) matches that number exactly; any text matches the subject (contains, case-insensitive, `%`/`_` literal). A numeric search matches the number **or** a subject containing those digits.
4. Every given filter must match (AND) (AC 4). Invalid values (unknown status, `createdFrom` after `createdTo`, `assigneeId` with `unassigned`, bad paging) → 400 with the field name.
5. `GET /api/tickets/assignees` (needs `tickets.view`) → active staff users (with a role) `{ id, fullName }` ordered by name — the assignee filter options (agents cannot call `/api/users`). CRM-16 reuses it for the assign picker.
6. Client: the Tickets page shows a filter bar (search, status, priority, category, assignee incl. "Unassigned", created from / to, "Clear filters"), a paged table (number, subject, customer, status, priority, category, assignee, created) and "New ticket". A ticket of a soft-deleted customer is listed with its customer name.

**Decisions**

- `ListTicketsQuery` (raw strings from the query string) is validated by `ListTicketsQueryValidator`, then `TicketService.ListAsync` turns it into a typed `TicketListFilter` passed to `ITicketRepository.ListAsync(filter, page, pageSize)`; the repository applies the filters on `Rows()` before paging.
- Date range = UTC calendar days: `createdFrom` → `CreatedAt >= from 00:00Z`; `createdTo` → `CreatedAt < (to + 1 day) 00:00Z`. The client sends the dates picked in `<input type="date">` as they are (documented edge case: a ticket created at 01:00 Riyadh time on the 7th is on the 6th in UTC).
- Filter changes apply immediately and reset to page 1; search applies on submit (like the customers page).

**Not in scope:** details page (CRM-15), assign / status actions (CRM-16 / CRM-17), saved views, sort by other columns, export, SLA columns.

---

## Context — Read These Files First

1. `CLAUDE.md` — Pagination, Endpoints, Frontend rules.
2. `.squad/stories/04-ticket-management/CRM-14/intake.md`.
3. `server/src/Crm.Infrastructure/Tickets/TicketRepository.cs` — `Rows()` (member-init `TicketRow` projection) and `GetViewAsync`; add `ListAsync` + `ListAssigneesAsync` here.
4. `server/src/Crm.Application/Tickets/TicketService.cs` (`ToResponse`), `ITicketRepository.cs`, `ITicketService.cs`, `TicketContracts.cs`, `TicketValues.cs` (`TryParseStatus`, `TryParsePriority`), `server/src/Crm.Domain/Tickets/Ticket.cs` (`TryParseNumber`).
5. `server/src/Crm.Application/Customers/ListCustomersQueryValidator.cs` (paging rules) and `server/src/Crm.Infrastructure/Customers/CustomerRepository.cs` lines 15–40 (`LikePattern`, count → order → skip/take).
6. `server/src/Crm.Infrastructure/Identity/UserService.cs` line 156 (`db.UserRoles` — users with roles).
7. `server/src/Crm.Api/Endpoints/TicketsEndpoints.cs` (group `/api/tickets`), `server/tests/Crm.Api.IntegrationTests/Tickets/TicketBodies.cs` (`TicketArrange`), `TicketsAuthorizationTests.cs` (policy dictionary: add the two routes, count 4).
8. `server/tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs` (`FakeTicketRepository`), `TicketServiceTests.cs`.
9. Client: `client/src/pages/tickets/TicketsPage.tsx` + test, `client/src/pages/customers/CustomersPage.tsx` (search + pager), `client/src/api/tickets.ts`, `client/src/api/paging.ts`, `client/src/features/tickets/useTickets.ts`, `client/src/features/customers/CustomerTimeline.tsx` line 85 (`Intl.DateTimeFormat(i18n.language, …)`), `client/src/test/fake-api.ts` (add `/api/tickets…`, `/api/ticket-categories` branches for the App tests).

---

## Backend Tasks

### 1 — Unit tests first (Red)

- **Create `server/tests/Crm.UnitTests/Tickets/ListTicketsQueryValidatorTests.cs`** — valid empty query; unknown `status` / `priority` → error on that field; `createdFrom` after `createdTo` → `createdTo`; `assigneeId` + `unassigned=true` → `assigneeId`; paging rules (`page=0`, `pageSize=101`).
- **Extend `TicketServiceTests.cs`** — `List_PassesTypedFilters_AndPaging_ToTheRepository` (status/priority parsed, dates → UTC bounds `from 00:00Z` / `to+1 00:00Z`, search trimmed, `SearchNumber` from `Ticket.TryParseNumber`), `List_UsesDefaultPaging`, `List_MapsViewsToResponses`, `List_InvalidQuery_ThrowsValidationException`.

### 2 — Application (Green)

- **`TicketContracts.cs`** — add:

```csharp
public sealed record ListTicketsQuery(
    string? Status, string? Priority, Guid? CategoryId, Guid? AssigneeId, bool? Unassigned,
    DateOnly? CreatedFrom, DateOnly? CreatedTo, string? Search, int? Page, int? PageSize);

public sealed record TicketListFilter(
    TicketStatus? Status, TicketPriority? Priority, Guid? CategoryId, Guid? AssigneeId, bool Unassigned,
    DateTime? CreatedFromUtc, DateTime? CreatedBeforeUtc, string? Search, int? SearchNumber);

public sealed record TicketAssigneeResponse(Guid Id, string FullName);
```

- **Create `ListTicketsQueryValidator.cs`**; **`TicketText`** gets `StatusField`, `StatusInvalid`, `CreatedToField`, `DateRangeInvalid`, `AssigneeField`, `AssigneeOrUnassigned`.
- **`ITicketRepository`** — `Task<PagedResult<TicketView>> ListAsync(TicketListFilter filter, int page, int pageSize, ct)`, `Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(ct)`.
- **`ITicketService` / `TicketService`** — `ListAsync(ListTicketsQuery, ct)`, `ListAssigneesAsync(ct)`; ctor gets `IValidator<ListTicketsQuery>`.

### 3 — Integration tests (Red)

**Create `server/tests/Crm.Api.IntegrationTests/Tickets/TicketListTests.cs`** — every test uses a unique subject tag + `search=<tag>` so the shared database does not leak; status / assignee / dates are set with `ExecuteUpdateAsync` on `CrmDbContext.Tickets` (no endpoint changes them before CRM-16/17):
- `List_WithoutParameters_IsPagedNewestFirst_WithDefaults` (AC 3: page 1, pageSize 20, `createdAt` descending),
- `List_Paginates_WithTotalCountOfAllMatches` (AC 3),
- `List_FiltersByStatus`, `List_FiltersByPriority`, `List_FiltersByCategory`, `List_FiltersByAssignee_AndUnassigned`, `List_FiltersByCreatedDateRange_Inclusive` (AC 1),
- `List_SearchByTicketNumber_InAnyForm`, `List_SearchBySubject_CaseInsensitive_WildcardsLiteral` (AC 2),
- `List_CombinedFilters_ReturnOnlyTicketsMatchingAll` (AC 4),
- `List_ShowsTicketsOfASoftDeletedCustomer` (CRM-8 AC 5),
- `List_WithInvalidFilters_Returns400` (theory: `status=done`, `priority=urgent`, `createdFrom=2026-10-05&createdTo=2026-10-01`, `unassigned=true&assigneeId=…`, `page=0`, `pageSize=101`),
- `Assignees_ListsActiveStaffUsers_ByName_ForAnAgent` (a deactivated user and a user without a role are not listed).
- **`TicketsAuthorizationTests`** — add `GET /api/tickets` and `GET /api/tickets/assignees` to the 401 / 403 theories and the policy dictionary (`tickets.view`, count 4).

### 4 — Infrastructure + Api (Green)

- **`TicketRepository.ListAsync`** — on `Rows()`: `Status ==`, `Priority ==`, `CategoryId ==`, `AssigneeId ==` / `AssigneeId == null`, `CreatedAt >=` / `<`, search `(SearchNumber != null && Number == SearchNumber) || EF.Functions.Like(Subject, LikePattern.Contains(search), LikePattern.EscapeCharacter)`; `CountAsync`; order `CreatedAt` desc, `Number` desc; `Skip/Take`; map rows to views.
- **`TicketRepository.ListAssigneesAsync`** — `db.Users.Where(u => u.IsActive && db.UserRoles.Any(r => r.UserId == u.Id)).OrderBy(FullName)`.
- **`TicketsEndpoints`** — `GET ""` (`[AsParameters] ListTicketsQuery`) and `GET "/assignees"` (group policy `tickets.view`).

---

## Frontend Tasks

### 1 — Tests first (Red)

- **`client/src/api/tickets.test.ts`** — `listTickets({})` → `/api/tickets`; all filters → `/api/tickets?status=open&priority=high&categoryId=k1&assigneeId=u1&createdFrom=2026-10-01&createdTo=2026-10-05&search=TKT-1&page=2&pageSize=20`; `unassigned: true` → `unassigned=true`; `listTicketAssignees()` → `/api/tickets/assignees`.
- **`client/src/pages/tickets/TicketsPage.test.tsx`** (mocks add `listTickets`, `listTicketAssignees`; existing new-ticket tests stay) — lists tickets newest first as returned with translated status / priority (AC 3); "No tickets found."; each filter calls `listTickets` with that filter and page 1 (AC 1); "Unassigned" option sends `unassigned: true`; search by number on submit (AC 2); combined filters are all sent (AC 4); "Clear filters"; pager.
- **`client/src/App.layout.test.tsx`** — "opens the tickets page from the sidebar" also sees a ticket row from the fake API.

### 2 — Implementation (Green)

- **`client/src/api/tickets.ts`** — `TicketListParams`, `listTickets`, `TicketAssignee`, `listTicketAssignees`.
- **`client/src/features/tickets/useTickets.ts`** — `useTickets(params)` (`keepPreviousData`), `useTicketAssignees()`.
- **Create `client/src/features/tickets/TicketFilters.tsx`** (controlled `TicketFilterValues`, `NativeSelect`s with "All" options, date inputs, "Clear filters") and **`TicketsTable.tsx`** (number `dir="ltr"`, created time with `Intl.DateTimeFormat(i18n.language, …)`).
- **`TicketsPage.tsx`** — search form + filters + table + pager + "New ticket".
- **`client/src/test/fake-api.ts`** — `/api/tickets?…` / `/api/tickets` page with one ticket, `/api/tickets/assignees`, `/api/ticket-categories…`.
- **i18n** — `tickets.searchLabel`, `search`, `filters.*` (status, priority, category, assignee, all, unassigned, createdFrom, createdTo, clear), `columns.*`, `empty`, `loading`, `pageInfo`, `previous`, `next`.

---

## Edge Cases & Failure Modes

- **Search "12"** → ticket TKT-000012 **and** tickets whose subject contains "12" (both are useful; documented).
- **Search "TKT-12a" / "%"** → treated as subject text; `%` / `_` literal (`LikePattern`).
- **`createdFrom` = `createdTo`** → that whole UTC day. `createdFrom > createdTo` → 400 `createdTo`.
- **Invalid date string** (`createdFrom=abc`) → 400 from parameter binding (ProblemDetails via the global handler).
- **`assigneeId` with `unassigned=true`** → 400 `assigneeId`. Unknown assignee / category id → simply no results.
- **Ticket of a soft-deleted customer** → still listed (projection ignores the soft-delete filter); a filter by that customer is out of scope.
- **Deactivated category** → still offered in the filter (full list) and shown on its tickets.
- **Same `CreatedAt`** → `Number` desc keeps the order stable across pages.

---

## Test Plan

1. Unit: `ListTicketsQueryValidatorTests.cs`, `TicketServiceTests.cs` (list).
2. Integration: `TicketListTests.cs` (AC 1–4, soft-deleted customer, assignees), `TicketsAuthorizationTests.cs` (4 routes); `PermissionPolicyTests` unchanged.
3. Client: `api/tickets.test.ts`, `pages/tickets/TicketsPage.test.tsx`, `App.layout.test.tsx`.

---

## Verification Steps

1. **Backend:** `cd server && dotnet build` (0 warnings) && `dotnet test`.
2. **Frontend:** `cd client && npm test && npm run build && npm run lint`.

---

## Done Criteria

- [ ] AC 1: status, priority, category, assignee (incl. unassigned) and date-range filters (API + UI).
- [ ] AC 2: search by ticket number (any form) or subject.
- [ ] AC 3: paged (`PagedResult`, default 20, max 100), newest first.
- [ ] AC 4: combined filters are ANDed (integration test).
- [ ] Tickets of soft-deleted customers listed; `/api/tickets/assignees` for agents.
- [ ] All strings in en + ar; builds / tests / lint green.

---

## How later stories build on this (write nothing here; for later planners)

- **CRM-15 (details):** link the number / subject cell of `TicketsTable` to `tickets/:id`.
- **CRM-16 (assign):** reuse `GET /api/tickets/assignees` / `useTicketAssignees()` for the assign picker; the list already filters and shows assignees.
- **CRM-17 (status):** status filter already works on all five statuses.
- **CRM-20 (SLA timers):** add due-time columns / an "overdue" filter to `TicketListFilter` and `TicketRow`.
