# Story 18 — Ticket history (audit trail) (Story: CRM-18)

## Prerequisites

- Story 16: [16-story-assign-ticket-CRM-16.md](16-story-assign-ticket-CRM-16.md) (`TicketHistoryEntry` table, `ITicketHistoryRecorder`, assignee entries) and story 17 (status entries). `main` (merged): `PUT /api/tickets/{id}/priority` (CRM-20) and `TicketSlaEvents` with `Escalated` rows (CRM-22).
- No new packages / migrations (the history table exists since CRM-16).

---

## Story Goal

A supervisor sees who changed what and when.

1. **Recording (AC 1).** Status (CRM-17) and assignee (CRM-16) already write entries. This story adds: **priority** (`TicketService.ChangePriorityAsync` records field `priority`, old / new codes, only when it really changes) and **category** — a new `PUT /api/tickets/{id}/category` body `{ categoryId }` (`tickets.view` + `tickets.manage`; null = no category; the category must be active (400 `categoryId`), unknown ticket 404, same category = no-op) recording field `category` with old / new **names**.
2. **Reading (AC 2).** `GET /api/tickets/{id}/history` (`tickets.view`) → array, **oldest first**: `{ id, field ("status"|"assignee"|"priority"|"category"|"escalation"), oldValue, newValue, changedById, changedByName, changedAt }`. The SLA `Escalated` events of the ticket (CRM-22) are merged in as `field: "escalation"`, `newValue` = the level, no user. Ordering: time, then insertion. Unknown ticket → 404.
3. **Read-only (AC 3).** The API has only the GET route for history: `POST` / `PUT` / `PATCH` / `DELETE` on `/api/tickets/{id}/history[/{entryId}]` → 404 / 405 (integration test asserts the endpoint list and the status codes). `TicketHistoryEntry` has no mutators.
4. **UI.** Details page gets tabs "Conversation" and "History" (plain `role="tablist"` buttons, no new library); the History tab lists entries with a translated field label, old → new value (status / priority values translated, "None" for empty), user (or "System") and time. A "Classify" row gives priority and category selects (`tickets.manage`) so the changes can actually be made from the UI.

**Decisions**

- `ITicketHistoryRepository.ListAsync(ticketId)` is implemented in `TicketHistoryRepository` on the one `CrmDbContext`, merging `TicketHistory` rows (with user names) and `TicketSlaEvents` of type `Escalated`; the history service maps to `TicketHistoryItemResponse`.
- `TicketHistoryField` gets no new value; `escalation` is only an API field name.
- `TicketService` gets `ITicketHistoryRecorder` (last ctor parameter).
- Category change lives in `TicketCategoryChangeService` (`ITicketCategoryChangeService`), history in `TicketHistoryService` (`ITicketHistoryService`).

**Not in scope:** paging / filtering, messages in the history, SLA breach / warning rows.

---

## Context — Read These Files First

1. `CLAUDE.md`; `.squad/stories/04-ticket-management/CRM-18/intake.md`; CRM-16 / 17 plans.
2. `Crm.Application/Tickets/TicketHistory.cs`, `TicketAssignmentService.cs`, `TicketService.cs` (`ChangePriorityAsync`), `Crm.Infrastructure/Tickets/TicketHistoryRepository.cs`, `Crm.Domain/Sla/TicketSlaEvent.cs`, `Crm.Application/Sla/SlaMonitorJob.cs`.
3. Client: `TicketDetailsPage.tsx`, `TicketThread.tsx`, `api/tickets.ts` (`changeTicketPriority`), `useTicketCategories`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `TicketServiceTests` / `TicketServiceSlaTests` — priority change records one `priority` entry (old / new); same priority records nothing.
- `TicketCategoryChangeServiceTests.cs` — change records `category` entry with names; to null records new = null; inactive / unknown category → `ValidationException` on `categoryId`; same category no-op; unknown ticket → not found.
- `TicketHistoryServiceTests.cs` — maps entries oldest first with user names; unknown ticket → not found.
- `Crm.Api.IntegrationTests/Tickets/TicketHistoryTests.cs` — status, assignee, priority and category changes each appear with old / new / user / time (AC 1); entries come oldest first (AC 2); an escalation (SLA job run with the fake clock past the due time) appears as `escalation` with the level; read-only: no write routes registered for history and `DELETE` / `PUT` / `POST` return 404 / 405 (AC 3); 404 for an unknown ticket; a ticket without changes returns `[]`.
- `TicketsAuthorizationTests` — `GET /api/tickets/{id}/history` (view) and `PUT /api/tickets/{id}/category` (view + manage) in the 401 / 403 theories and the policy dictionary.

### 2 — Implementation (Green)

- Application: `TicketHistoryContracts` (`TicketHistoryItemResponse`), `ITicketHistoryService` / `TicketHistoryService`, `ITicketHistoryRepository.ListAsync`, `ITicketCategoryChangeService` / service + `ChangeTicketCategoryRequest`, priority recording in `TicketService`, texts, DI.
- Infrastructure: `TicketHistoryRepository.ListAsync`.
- Api: `TicketHistoryEndpoints` (`GET /api/tickets/{id}/history`, `PUT /api/tickets/{id}/category`).

---

## Frontend Tasks

### 1 — Tests first (Red)

- `api/tickets.test.ts` — `getTicketHistory`, `changeTicketCategory`.
- `TicketDetailsPage.test.tsx` — History tab lists entries in the order returned with field label, translated old → new, user and time; escalation entry shows the level; empty state; priority / category selects send their changes; hidden without `tickets.manage`.

### 2 — Implementation (Green)

- `api/tickets.ts`, `features/tickets/TicketHistory.tsx`, `TicketClassifyControl.tsx`, tabs in the details page, `useTickets` hooks, i18n `tickets.history.*` (en + ar).

---

## Verification Steps

`cd server && dotnet build && dotnet test`; `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1–3 covered by unit + integration (+ client) tests; escalations shown; strings in en + ar.

## How later stories build on this

- New ticket changes must call `ITicketHistoryRecorder`; new SLA event kinds can be merged in `TicketHistoryRepository.ListAsync`.

## Deviations (as built)

(none yet)
