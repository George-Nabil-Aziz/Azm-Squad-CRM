# Story 16 — Assign ticket to agent (Story: CRM-16)

## Prerequisites

- Story 15 done: [15-story-ticket-details-replies-CRM-15.md](15-story-ticket-details-replies-CRM-15.md) (`ITicketRepository.FindAsync` tracked ticket, details page `tickets/:id`, `TicketReplyForm` pattern). Story 14: `GET /api/tickets/assignees` (active users with a role) and the assignee list filter. CRM-7: `Permissions.TicketsAssign`, `ICurrentUser.HasPermission`.
- No new packages / shadcn components.

---

## Story Goal

Every ticket gets a clear owner.

1. `POST /api/tickets/{id}/assign` body `{ assigneeId }` (`tickets.view` + `tickets.manage`) → 200 with the updated `TicketResponse`. `assigneeId: null` unassigns.
2. **Who may assign** (AC 2): a user with `tickets.assign` (Supervisor, Admin, SuperAdmin) may assign to anyone or unassign. A user without it (Agent) may only **take the ticket for themselves** (`assigneeId` = own id) or **release their own ticket** (`null` while they are the assignee); anything else → `ForbiddenException` (403).
3. **Assignee rules** (AC 3): the target must be an active staff user with a role — the same set as `GET /api/tickets/assignees`. Unknown, inactive or role-less user → 400 on `assigneeId`. Missing field in the body → treated as null (unassign); a malformed GUID → 400 from binding.
4. **History** (AC 1): a change writes a history entry (field `assignee`, old / new = user full names, null = unassigned, by the current user, UTC time) through the new `ITicketHistoryRecorder`, saved in the same unit of work as the ticket change. Assigning to the current assignee is a no-op: 200, no entry, `UpdatedAt` unchanged.
5. **The assigned agent sees the ticket** (AC 4): `GET /api/tickets?assigneeId=<me>` already filters; the integration test proves it. Client: "My tickets" is the existing assignee filter (the page offers the current assignee list).
6. Unknown ticket → 404. A closed ticket can still be assigned.

**Decisions**

- Domain: `Ticket.AssignTo(Guid? assigneeId, DateTime utcNow)` returns true when it changed (sets `AssigneeId`, `UpdatedAt`).
- **History storage (shared contract):** `TicketHistoryEntry` (`long Id` identity, `TicketId`, `Field` (`TicketHistoryField`: Status, Assignee, Priority, Category; stored by name), `OldValue?`, `NewValue?` (display snapshots, max 200: status / priority = API codes ("open", "high"), assignee / category = names), `ChangedById?`, `ChangedAt`). Immutable: no update / delete method, no endpoint (CRM-18 AC 3).
- `ITicketHistoryRecorder.Record(Guid ticketId, TicketHistoryField field, string? oldValue, string? newValue, DateTime utcNow)` — adds to the unit of work (like `IInteractionRecorder`), actor = `ICurrentUser.UserId` (null for system / SLA). Implementation `TicketHistoryRecorder` over `ITicketHistoryRepository.Add`. CRM-17 / 18 and the SLA group call it.
- Names for the history come from `ITicketRepository.FindAssigneeAsync(userId)` → `TicketAssigneeResponse?` (active + role) and the current assignee name from `GetViewAsync`.
- Service: `ITicketAssignmentService.AssignAsync(ticketId, AssignTicketRequest)` (`AssignTicketRequest(Guid? AssigneeId)`); text in `TicketText` (`AssigneeField`, `AssigneeUnavailable`, `AssignForbidden`).
- Migration `AddTicketHistory` (table `TicketHistory`, index `(TicketId, ChangedAt)`).

**Not in scope:** automatic assignment, notifications, history read API / tab (CRM-18).

---

## Context — Read These Files First

1. `CLAUDE.md`; `.squad/stories/04-ticket-management/CRM-16/intake.md`; CRM-15 plan (How later stories build on this).
2. `server/src/Crm.Application/Tickets/TicketMessageService.cs` (find tracked ticket → change → record → `SaveChangesAsync`), `Crm.Application/Customers/Timeline/InteractionRecorder.cs` (recorder pattern), `RolePermissions.cs`, `TicketRepository.cs` (`ListAssigneesAsync`).
3. Tests: `TicketTestDoubles.cs`, `TicketMessagesTests.cs` (integration style), `TicketsAuthorizationTests.cs`; `CrmApiFactory.CreateClientWithRoleAsync`, `CreateUserAsync`; user deactivate endpoint of CRM-6 (for the inactive user test).
4. Client: `TicketDetailsPage.tsx`, `TicketReplyForm.tsx`, `useTickets.ts` (`useTicketAssignees`), `features/auth/Can.tsx`, `useCurrentUser.ts`, `NativeSelect`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `TicketTests.cs` — `AssignTo` sets assignee + `UpdatedAt`, returns false when unchanged (UpdatedAt kept), rejects non-UTC.
- `TicketHistoryEntryTests.cs` — factory trims / shortens values, UTC, `ChangedById` optional.
- `TicketAssignmentServiceTests.cs` — supervisor assigns → assignee updated + one history entry (old/new names, actor, time) (AC 1); unassign records new = null; same assignee → no entry; agent assigning to someone else → `ForbiddenException`, nothing changed (AC 2); agent takes the ticket for self / releases own; agent unassigning someone else's → forbidden; inactive / unknown assignee → `ValidationException` on `assigneeId` (AC 3); unknown ticket → `NotFoundException`.
- `Crm.Api.IntegrationTests/Tickets/TicketAssignmentTests.cs` — supervisor assigns an agent → `GET /api/tickets/{id}` shows assignee and the history row exists in the database (AC 1); agent assigning another user → 403 ProblemDetails (AC 2); deactivated user → 400 `assigneeId` (AC 3); assignee sees the ticket via `GET /api/tickets?assigneeId=<id>` and in `unassigned=true` no more (AC 4); agent self-assign 200; 404; unassign.
- `TicketsAuthorizationTests` — add `POST /api/tickets/{id}/assign` (view + manage; count 7) to the 401 / 403 theories and policy dictionary.

### 2 — Domain, Application, Infrastructure, Api (Green)

- Domain: `TicketHistoryField`, `TicketHistoryEntry`, `Ticket.AssignTo`.
- Application: `ITicketHistoryRecorder` + `TicketHistoryRecorder`, `ITicketHistoryRepository`, `ITicketAssignmentService` + `TicketAssignmentService`, `AssignTicketRequest`, `ITicketRepository.FindAssigneeAsync`, texts, DI.
- Infrastructure: `TicketHistoryEntryConfiguration`, `TicketHistoryRepository`, `DbSet`, DI, migration `AddTicketHistory`.
- Api: `POST /api/tickets/{id}/assign` in a new `TicketAssignmentEndpoints`.

---

## Frontend Tasks

### 1 — Tests first (Red)

- `api/tickets.test.ts` — `assignTicket('t1', 'u1')` → `POST /api/tickets/t1/assign` `{ assigneeId: 'u1' }`; `null` sends `{ assigneeId: null }`.
- `TicketDetailsPage.test.tsx` — supervisor sees the assign select (assignees + "Unassigned") and "Assign", choosing an agent sends it and reloads; an agent (no `tickets.assign`) sees no select but an "Assign to me" button that sends the own id, hidden when the ticket is already theirs; server error text is shown.

### 2 — Implementation (Green)

- `api/tickets.ts` `assignTicket`; `features/tickets/TicketAssignControl.tsx` (`<Can permission={ticketsAssign}>` select + button; `<Can permission={ticketsManage}>` "Assign to me"); used in the details header; i18n `tickets.details.assign*` (en + ar).

---

## Verification Steps

`cd server && dotnet build && dotnet test`; `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1–4 covered by unit + integration (+ client) tests; migration `AddTicketHistory` applies; strings in en + ar.

## How later stories build on this

- **CRM-17 (status)** and **CRM-18 (history):** call `ITicketHistoryRecorder.Record(..., TicketHistoryField.Status | Priority | Category, ...)`; CRM-18 adds `ITicketHistoryRepository.ListAsync` + the API / tab.
- **SLA / automatic assignment:** call `Ticket.AssignTo` + `ITicketHistoryRecorder` (actor null).

## Deviations (as built)

(none yet)
