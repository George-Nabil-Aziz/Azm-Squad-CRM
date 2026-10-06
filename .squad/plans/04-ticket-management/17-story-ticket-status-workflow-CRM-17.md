# Story 17 — Ticket status workflow (Story: CRM-17)

## Prerequisites

- Story 16 done: [16-story-assign-ticket-CRM-16.md](16-story-assign-ticket-CRM-16.md) (`ITicketHistoryRecorder`, `TicketHistoryField.Status`, tracked `ITicketRepository.FindAsync`, `TicketTestSupport` reflection helper which this story replaces). Story 15: closed tickets take no messages.
- No new packages / shadcn components.

---

## Story Goal

Tickets move through one consistent flow.

1. **Transitions** (Domain `TicketStatusRules`, no database):

| From | Allowed targets |
|------|-----------------|
| New | Open |
| Open | Pending, Resolved |
| Pending | Open, Resolved |
| Resolved | Closed, Open (reopen) |
| Closed | Open (reopen) |

2. `PUT /api/tickets/{id}/status` body `{ status }` (`tickets.view` + `tickets.manage`) → 200 `TicketResponse` (AC 1). Unknown status name, the current status, or a transition not in the table (e.g. Closed → Pending) → 400 on `status` with the allowed targets in the message (AC 2). Unknown ticket → 404.
3. Moving to **Resolved** sets `Ticket.ResolvedAt` (UTC `DateTime?`, shared contract name) (AC 3). Moving to **Open** from Resolved or Closed (**reopen**) clears it (AC 4). Other transitions leave it unchanged.
4. Every change writes a history entry through `ITicketHistoryRecorder` (field `status`, old / new = API codes), saved with the change; a rejected transition writes nothing.
5. `TicketResponse` gains `resolvedAt` and `allowedStatuses` (API names the user may move to now; the UI shows one action per entry).
6. Client: the details page shows status actions (`<Can>` tickets.manage): "Open", "Set pending", "Resolve", "Close", "Reopen" (target Open from Resolved / Closed); the closed-ticket notice already points to "Reopen". `resolvedAt` is shown next to "First response".

**Decisions**

- `Ticket.ChangeStatus(TicketStatus to, DateTime utcNow)` throws `InvalidTicketStatusTransitionException` (Domain) for an illegal move; the service checks `TicketStatusRules.CanMove` first and throws a `ValidationException` (400), so the exception only guards other callers (SLA, channels).
- Moving to the same status is invalid (400), not a silent success.
- Migration `AddTicketResolvedAt` (`Tickets.ResolvedAt` nullable).
- Customer timeline: no entry (the history is the audit trail; CRM-10 only lists `ticketCreated` for tickets).

**Not in scope:** auto-close, portal reopen, SLA pause / stop, notifications.

---

## Context — Read These Files First

1. `CLAUDE.md`; `.squad/stories/04-ticket-management/CRM-17/intake.md`; the CRM-15 and CRM-16 plans (How later stories build on this).
2. `server/src/Crm.Application/Tickets/TicketAssignmentService.cs` (find → rules → record history → save pattern), `TicketService.ToResponse`, `TicketValues`, `TicketText`.
3. `server/src/Crm.Domain/Tickets/Ticket.cs`, `TicketStatus.cs`; tests `TicketTests.cs`, `TicketTestDoubles.cs` (`TicketTestSupport.SetStatus`).
4. Client: `TicketDetailsPage.tsx`, `TicketAssignControl.tsx`, `ticket-values.ts`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `TicketStatusRulesTests.cs` — the whole table: every (from, to) pair among the five statuses is allowed or not exactly as above (theory over all 25 pairs); `AllowedTargets` order.
- `TicketTests.cs` — `ChangeStatus` sets status + `UpdatedAt`; Resolved sets `ResolvedAt` (UTC); reopen from Resolved and Closed clears it; Open → Pending keeps it; illegal move throws and changes nothing; same status throws.
- `TicketStatusServiceTests.cs` — valid transition succeeds and records one history entry (old / new codes) (AC 1); Closed → Pending → `ValidationException` on `status`, no history, no save (AC 2); Resolve sets `ResolvedAt` (AC 3); reopen clears it (AC 4); unknown status string → validation; unknown ticket → not found; response carries `allowedStatuses` and `resolvedAt`.
- `Crm.Api.IntegrationTests/Tickets/TicketStatusTests.cs` — walk New → Open → Pending → Open → Resolved → Closed → Open through the API (AC 1 / 3 / 4: `resolvedAt` set after Resolved, null after reopen); Closed → Pending 400 with `status` error (AC 2); a rejected move leaves status unchanged and writes no history row; history rows (field `status`, old / new, user); a closed ticket takes replies again after reopen; 404; `allowedStatuses` on `GET /api/tickets/{id}`.
- `TicketsAuthorizationTests` — add `PUT /api/tickets/{id}/status` (view + manage; count 8).

### 2 — Implementation (Green)

- Domain: `TicketStatusRules`, `InvalidTicketStatusTransitionException`, `Ticket.ResolvedAt`, `Ticket.ChangeStatus`.
- Application: `ChangeTicketStatusRequest(string? Status)`, `ITicketStatusService` / `TicketStatusService`, `TicketResponse` (+ `ResolvedAt`, `AllowedStatuses`), `TicketText` (`StatusField`, `StatusTransitionInvalid`, `StatusSame`), DI.
- Infrastructure: migration `AddTicketResolvedAt`.
- Api: `PUT /api/tickets/{id}/status` in `TicketStatusEndpoints`.
- Tests: `TicketTestSupport.SetStatus` stays only for the closed-ticket message tests (or uses `ChangeStatus` where the path is legal).

---

## Frontend Tasks

### 1 — Tests first (Red)

- `api/tickets.test.ts` — `changeTicketStatus('t1', 'resolved')` → `PUT /api/tickets/t1/status` `{ status: 'resolved' }`.
- `TicketDetailsPage.test.tsx` — an open ticket shows "Set pending" and "Resolve" (from `allowedStatuses`) and not "Close"; clicking sends the status; a resolved ticket shows "Close" and "Reopen" (target open); a closed ticket shows only "Reopen"; users without `tickets.manage` see no actions; a 400 `status` message is shown; `resolvedAt` is shown when set.

### 2 — Implementation (Green)

- `api/tickets.ts` (`changeTicketStatus`, `resolvedAt`, `allowedStatuses`), `features/tickets/TicketStatusActions.tsx`, details page wiring, i18n `tickets.details.statusActions.*`, `resolvedAt` / `notResolved` labels (en + ar); test factories carry the two new fields.

---

## Verification Steps

`cd server && dotnet build && dotnet test`; `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1–4 covered by unit + integration (+ client) tests; migration applies; strings in en + ar.

## How later stories build on this

- **CRM-18:** status entries already exist in the history table (field `status`); the History tab only has to list them.
- **SLA group:** read `ResolvedAt`; to move a ticket call `Ticket.ChangeStatus` + `ITicketHistoryRecorder` (actor null).

## Deviations (as built)

(none yet)
