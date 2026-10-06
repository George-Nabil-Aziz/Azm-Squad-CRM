# Story 15 — Ticket details & replies (Story: CRM-15)

## Prerequisites

- Stories 12–14 on `main` (CRM-12 categories, CRM-13 `Ticket` / `TicketView` / `GET /api/tickets/{id}`, CRM-14 list). Their "How later stories build on this" is binding: the number cell of `TicketsTable` links to `tickets/:id`; `TicketRepository.Rows()` is the ticket projection.
- CRM-10: `IInteractionRecorder.Record(...)` is called **before** the unit of work is saved (`InteractionType.Message`).
- No new packages, no new shadcn component.

---

## Story Goal

An agent handles a request on one screen: `tickets/:id` shows the ticket, its conversation, a reply box and an internal-note switch.

1. `GET /api/tickets/{id}/messages` (`tickets.view`) → the thread, **oldest first**: `id, direction (inbound|outbound|internal), body, channel, authorId, authorName (null = the customer / system), createdAt, deliveryStatus?` (AC 1).
2. `POST /api/tickets/{id}/messages` (`tickets.manage`) body `{ body, internal }` (`internal` default false) → 201 with the message. `internal=false` creates an **Outbound** reply on the ticket's channel; `internal=true` an **InternalNote** (AC 2). Blank / over 10 000 characters → 400 `body`. Unknown ticket → 404.
3. The first public agent reply sets `Ticket.FirstResponseAt` (UTC); later replies, internal notes and inbound messages never change it (AC 3).
4. A `Closed` ticket accepts no reply and no note: 400 on field `status` (AC 4). (Reopening arrives with CRM-17.)
5. Internal notes are never customer-visible: they are never dispatched, never recorded in the customer timeline, and `ITicketMessageService.ListCustomerVisibleAsync` (used by the future portal / channel stories) excludes them (AC 2).
6. After saving an outbound reply the service calls `ITicketReplyDispatcher` (no-op now; the channel group implements it).
7. `GET /api/tickets/{id}` also returns `firstResponseAt`.

**Decisions**

- Entity `TicketMessage` (Domain): `Id, TicketId, Direction (MessageDirection), AuthorId?, Body, Channel (TicketChannel), CreatedAt, DeliveryStatus? (MessageDeliveryStatus Pending/Sent/Failed), ExternalMessageId?`. `TicketMessage.Staff(...)` factory for reply / note, `TicketMessage.Inbound(...)` for channel stories; `MarkSent(externalId)` / `MarkFailed()` for the dispatcher. Delivery status is `Pending` for outbound replies on a non-Manual channel, null otherwise.
- `Ticket.FirstResponseAt` (`DateTime?`), `Ticket.AcceptsMessages` (status ≠ Closed), `Ticket.RecordAgentReply(utcNow)` (sets `FirstResponseAt` once, bumps `UpdatedAt`). Shared contract name — keep exactly.
- `ITicketMessageRepository` (`Add`, `ListAsync(ticketId, includeInternal)`) and a tracked `ITicketRepository.FindAsync(id)`; both share the scoped `CrmDbContext`, saved once by `ITicketRepository.SaveChangesAsync` (message + ticket + timeline entry together).
- Timeline: an outbound reply records `InteractionEvents.MessageSent` ("messageSent", details = first 200 chars, source = message id); internal notes record nothing.
- `ITicketReplyDispatcher.DispatchAsync(TicketMessage, CancellationToken)`; `NoopTicketReplyDispatcher` registered with `TryAddScoped` so the channel group replaces it. Called only for outbound replies, after the save.
- Messages are not paged (one ticket's thread); revisit with CRM-23.
- Migration `AddTicketMessages` (new table + `Tickets.FirstResponseAt`).

**Not in scope:** real delivery, message attachments, assign / status (CRM-16/17), history tab (CRM-18), portal.

---

## Context — Read These Files First

1. `CLAUDE.md`; `.squad/stories/04-ticket-management/CRM-15/intake.md`.
2. `server/src/Crm.Domain/Tickets/Ticket.cs`, `server/src/Crm.Application/Tickets/TicketService.cs` / `TicketContracts.cs` / `TicketText.cs`, `Crm.Infrastructure/Tickets/TicketRepository.cs`, `TicketConfiguration.cs`.
3. `Crm.Application/Customers/Notes/CustomerNoteService.cs` (service + recorder + save pattern), `Crm.Domain/Customers/InteractionEvents.cs`.
4. Tests: `tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs`, `TicketsAuthorizationTests.cs`, `TicketBodies.cs`, `CrmApiFactory` (`Time`, `CreateClientWithRoleAsync`).
5. Client: `client/src/pages/customers/CustomerDetailsPage.tsx` + `features/customers/CustomerNotes.tsx` (form pattern), `features/tickets/TicketsTable.tsx`, `app/AppRoutes.tsx`, `api/tickets.ts`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Tickets/TicketMessageTests.cs` — factory rules (body trimmed / required / max, UTC), delivery status defaults, `MarkSent` / `MarkFailed`.
- `TicketTests.cs` — `RecordAgentReply` sets `FirstResponseAt` once; `AcceptsMessages` false only for Closed.
- `TicketMessageServiceTests.cs` — reply is Outbound with author + time; note is InternalNote; first reply sets `FirstResponseAt`, second keeps it, note does not set it (AC 1–3); Closed → `ValidationException` (AC 4); unknown ticket → `NotFoundException`; dispatcher called for replies only; timeline entry for replies only; blank body → validation; `ListCustomerVisibleAsync` excludes notes (AC 2).
- `Crm.Api.IntegrationTests/Tickets/TicketMessagesTests.cs` — reply shows in thread with author name and time (AC 1); note flagged `internal`, absent from `ListCustomerVisibleAsync` and from the customer timeline (AC 2); `firstResponseAt` on `GET /api/tickets/{id}` after the first reply, unchanged after the second (AC 3); Closed (set via `ExecuteUpdateAsync`) → 400 (AC 4); 404; blank body 400; thread chronological.
- `TicketsAuthorizationTests` — add `GET` / `POST /api/tickets/{id}/messages` (view / manage; count 6).

### 2 — Domain, Application, Infrastructure, Api (Green)

- Domain: `MessageDirection`, `MessageDeliveryStatus`, `TicketMessage`, `Ticket` additions, `InteractionEvents.MessageSent`.
- Application: `TicketMessageContracts.cs` (`AddTicketMessageRequest(string? Body, bool? Internal)`, `TicketMessageResponse`), validator, `ITicketMessageService` / `TicketMessageService`, `ITicketMessageRepository`, `ITicketReplyDispatcher` + `NoopTicketReplyDispatcher`, `TicketMessageText`, `TicketValues` message names; `TicketResponse` gets `FirstResponseAt`; `ITicketRepository.FindAsync`; DI.
- Infrastructure: `TicketMessageConfiguration` (table `TicketMessages`, enums by name, FK ticket + author Restrict, index `(TicketId, CreatedAt)`), `TicketMessageRepository`, DbSet, DI, migration `AddTicketMessages`.
- Api: `TicketMessagesEndpoints` (`/api/tickets/{id}/messages`).

---

## Frontend Tasks

### 1 — Tests first (Red)

- `api/tickets.test.ts` — `listTicketMessages(id)`, `addTicketMessage(id, { body, internal })`.
- `pages/tickets/TicketDetailsPage.test.tsx` — shows ticket header + thread with author and time (AC 1); internal notes carry an "Internal note" badge (AC 2); reply sends `{ body, internal: false }`; the toggle sends `internal: true`; blank body shows an error and sends nothing; a closed ticket shows "closed" notice and no reply box (AC 4); unknown id → not found.
- `TicketsPage` test — the number links to the details page.

### 2 — Implementation (Green)

- `api/tickets.ts` (`TicketMessage`, calls, `firstResponseAt`), `features/tickets/useTicketMessages.ts`, `TicketThread.tsx`, `TicketReplyForm.tsx`, `pages/tickets/TicketDetailsPage.tsx`, route `tickets/:id`, link in `TicketsTable`, i18n (`tickets.details.*`, en + ar).

---

## Verification Steps

`cd server && dotnet build && dotnet test`; `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1–4 each covered by unit + integration (+ client) tests; migration applied cleanly; strings in en + ar.

## How later stories build on this

- **CRM-16 / 17 / 18** add controls and a History tab to `TicketDetailsPage`; they call `ITicketRepository.FindAsync` (tracked) and save through it.
- **Channel stories (CRM-23..26):** implement `ITicketReplyDispatcher` (replace the no-op registration), call `TicketMessage.MarkSent / MarkFailed`, create inbound messages with `TicketMessage.Inbound(...)` (they must not set `FirstResponseAt`).
- **SLA group:** `Ticket.FirstResponseAt` is set here; read it, do not set it elsewhere.

## Deviations (as built)

(none yet)
