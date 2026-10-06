# Story 42 — Customer portal: track requests & history (Story: CRM-42)

## Prerequisites

- Stories 40, 41 (portal login and submit), 15 (messages), 17 (status workflow), 18 (history).
- CRM 01 reference (read-only): `specs/36-track-requests/`, `specs/37-view-history-portal/`.

## Story Goal

1. `GET /api/portal/tickets` lists **only the signed-in customer's tickets** (newest first, paged); `GET /api/portal/tickets/{id}` of another customer's ticket (or an unknown one) is **404** (AC 1).
2. The ticket detail shows the **status and the public replies only**: `GET /api/portal/tickets/{id}/messages` never contains internal notes; the customer-facing history (`GET .../history`) is "created" plus status changes, without assignee, priority or SLA data (AC 2).
3. The customer **replies** on an open ticket (`POST .../messages { body }`): stored as an inbound Portal message, bumps the ticket, shows in the customer timeline; a Pending ticket becomes Open again (recorded in the history); a Resolved or Closed ticket → 400 on `status` (AC 3).
4. The customer **reopens a resolved ticket within `Portal:ReopenWindowDays`** (default 7) of `ResolvedAt` (`POST .../reopen`): Resolved → Open (history entry, resolution timer runs again); later than that, or any other status → 400 on `status` (AC 4).
5. Client: "My requests" list (`/portal/tickets`) and detail page (`/portal/tickets/:id`) with the conversation, history, reply box and reopen button.

## Design

- **Application** `PortalTicketReadService` / `IPortalTicketTracker` in `Crm.Application/Portal`: `ListAsync(customerId, page, pageSize)`, `GetAsync`, `ListMessagesAsync`, `ListHistoryAsync`, `ReplyAsync`, `ReopenAsync`. Ownership check in one place (`FindOwnTicket`: ticket.CustomerId == customerId else `NotFoundException`).
- `TicketListFilter` gets an optional `CustomerId`; `TicketRepository.ListAsync` applies it. Portal ticket shape `PortalTicketSummary(Id, Number, Subject, Status, CategoryName, CreatedAt, UpdatedAt, CanReply, CanReopen)` (no assignee, priority or SLA fields).
- Reopen window logic in Domain: `Ticket.CanBeReopenedByCustomer(utcNow, window)` (Resolved and `utcNow - ResolvedAt <= window`).
- **API** `PortalTicketsEndpoints` (Portal policy) extended. **No migration.**

## Backend Tasks

1. Tests first: unit `TicketReopenWindowTests` (boundary at exactly 7 days, not resolved, closed), `PortalTicketTrackerTests` (own tickets only, other customer's → NotFound, messages exclude internal notes, reply on New / Open / Pending OK and Pending → Open with history, reply on Resolved / Closed → validation, blank reply → validation, reopen inside / outside the window, reopen of an Open ticket, history shows created + status only), integration `PortalTrackTicketsTests` (AC 1–4 over HTTP with two customers, staff internal note invisible, staff reply visible, reopen window with the fake clock, staff status change appears in the history, staff token 403).
2. Implement.

## Frontend Tasks

1. Tests first: list shows the customer's tickets with status; detail shows public replies, reply posts and refreshes, reopen button only for a resolved ticket inside the window (`canReopen`), 404 shows "not found".
2. `api/portal.ts` additions, `PortalTicketsPage`, `PortalTicketDetailsPage`, routes under `RequirePortalAuth`, i18n.

## Edge cases

- A customer reply never changes `FirstResponseAt` (only agents do). Replies on a Pending ticket reopen it; on New / Open the status stays.
- "Allowed days" = `Portal:ReopenWindowDays`, counted in 24-hour days from `ResolvedAt` (UTC).

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.
