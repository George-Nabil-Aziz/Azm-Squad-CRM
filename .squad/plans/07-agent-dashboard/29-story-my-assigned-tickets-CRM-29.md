# Story 29 — My assigned tickets (Story: CRM-29)

## Prerequisites

- Ticket management (CRM-13..18), SLA (CRM-20..22: due times, `TicketSlaEvents`), CRM-27/28 on this branch. `Ticket.AssigneeId`, `TicketView`, `TicketService.ToResponse`, `TicketStatus { New, Open, Pending, Resolved, Closed }`.
- No new permission (`tickets.view`), no migration.

---

## Story Goal

An agent sees their own work first on the dashboard.

1. `GET /api/tickets/mine?page&pageSize` returns the tickets **assigned to the current user that are not Closed** (AC 1) sorted by the **nearest SLA due time** first (AC 2), plus counters (AC 3).
2. **Next SLA due** (`Ticket.NextSlaDueAt`, Domain): resolved tickets have none; before the first response the earlier of `ResponseDueAt` / `ResolutionDueAt`; after it `ResolutionDueAt`. Tickets without a due time sort last (then by `CreatedAt`, `Number`).
3. **Counters**: `open` = status New or Open, `pending` = status Pending (both of my non-Closed tickets), `breachedToday` = my non-Closed tickets with a response or resolution breach event (`TicketSlaEvents`) that occurred on the current UTC day.
4. UI (AC 4): the dashboard shows three counters and a table "My tickets" (number link to `/tickets/{id}`, subject, customer, priority, status, SLA timers) in the server order; the existing welcome and API-status cards stay.

**Not in scope:** supervisor / team views, charts, time zones other than UTC for "today".

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/07-agent-dashboard/CRM-29/intake.md`.
2. `server/src/Crm.Domain/Tickets/Ticket.Sla.cs` (due times, `FirstResponseAt`, `ResolvedAt`), `Crm.Application/Tickets/TicketService.cs` (`ToResponse`), `TicketContracts.cs`, `ITicketRepository.cs`, `Crm.Infrastructure/Tickets/TicketRepository.cs` (`Rows()`), `Crm.Domain/Sla/TicketSlaEvent.cs`, `SlaEventType.cs`.
3. `server/src/Crm.Api/Endpoints/TicketsEndpoints.cs` (route group), `tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs`.
4. Client: `pages/dashboard/DashboardPage.tsx` + test, `features/tickets/TicketsTable.tsx`, `api/tickets.ts`, `features/sla/TicketSlaTimers.tsx`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Sla/TicketNextDueTests.cs`: `NextSlaDueAt_BeforeFirstResponse_IsTheEarlierDue`, `_AfterFirstResponse_IsResolutionDue`, `_Resolved_IsNull`, `_WithoutPolicy_IsNull`.
- `Crm.UnitTests/Tickets/MyTicketsServiceTests.cs` (fake repository): `OnlyMyNotClosedTickets` (AC 1), `SortedByNextDue_NullsLast` (AC 2), `Counters_OpenPendingBreachedToday` (AC 3), paging, anonymous user rejected.
- `Crm.Api.IntegrationTests/Tickets/MyTicketsTests.cs`: assigned tickets of two agents with different priorities and a Closed one → only mine, High before Low, counters; `breachedToday` after the job ran past the due time; another agent's tickets never show; anonymous 401.

### 2 — Domain / Application

- `Ticket.NextSlaDueAt` (Ticket.Sla.cs).
- `Crm.Application/Tickets/MyTickets.cs`: `MyTicketsQuery(int? Page, int? PageSize)`, `MyTicketCounters(int Open, int Pending, int BreachedToday)`, `MyTicketsResponse(MyTicketCounters Counters, PagedResult<TicketResponse> Tickets)`, `IMyTicketsRepository` (`ListOpenAssignedAsync(userId)` → `TicketView`s not Closed, `CountBreachedTodayAsync(userId, dayStartUtc, dayEndUtc)`), `IMyTicketsService` + `MyTicketsService` (uses `ICurrentUser`, `TimeProvider`).

### 3 — Infrastructure / Api

- `MyTicketsRepository` (EF Core, reuses `TicketRepository.Rows()` semantics), DI, `GET /api/tickets/mine` in `TicketsEndpoints` (`tickets.view`; declared before `/{id:guid}`).

---

## Frontend Tasks

- Tests first: `DashboardPage.test.tsx` (counters, table order as returned, ticket link, empty state, hidden without `tickets.view`), `api/tickets.test.ts` (`getMyTickets`).
- `api/tickets.ts` `getMyTickets`, `features/dashboard/MyTickets.tsx` + `useMyTickets.ts`, `DashboardPage` extended, i18n `dashboard.myTickets.*` (en + ar).

---

## Edge Cases & Failure Modes

- No assigned tickets: counters 0, empty message.
- Breach events of a ticket reassigned later count for the current assignee only (events are joined to the ticket's current assignee).
- A ticket with both breaches today counts once (`Distinct` ticket ids).
- User without `tickets.view`: 403 from the API; the dashboard hides the section (`<Can>`).

## Test Plan

Unit (`TicketNextDueTests`, `MyTicketsServiceTests`), integration (`MyTicketsTests`), client (`DashboardPage`, `tickets` API).

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`.
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: only my tickets that are not Closed.
- [ ] AC 2: nearest SLA due first.
- [ ] AC 3: counters open, pending, breached today.
- [ ] AC 4: a ticket opens its details.
- [ ] build / tests / lint green.
