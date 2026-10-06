# Story 30 — Customer info side panel (Story: CRM-30)

## Prerequisites

- Customers with contacts (CRM-8/9), tickets (CRM-13/14), ticket details page (`TicketDetailsPage`). Permissions: reads need `tickets.view` **and** `customers.view` (the panel shows customer data); both exist, no new permission, no migration.

---

## Story Goal

The ticket page shows who the customer is without leaving it.

1. `GET /api/tickets/{id}/customer-context` returns the ticket's **current** customer (name, email, phone, all contacts), the **total number of the customer's tickets** (AC 1) and the **last 5 tickets** of the customer, newest first, each with number, subject, status, priority, created time, and a flag for the open ticket (AC 2). A deleted customer is still shown (`customerDeleted: true`), like on the ticket.
2. UI: a side panel on the ticket page (right column on wide screens, below the details on narrow ones) with the data, a link to `/customers/{id}` (AC 3) and links to the listed tickets.
3. AC 4: the panel's query is keyed by the ticket id **and** `ticket.customerId`; the server always reads the ticket's current customer, so a changed customer shows up as soon as the ticket reloads (test: re-render with another `customerId`).

**Not in scope:** editing the customer, changing a ticket's customer (no endpoint).

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/07-agent-dashboard/CRM-30/intake.md`.
2. `server/src/Crm.Application/Customers/CustomerService.cs` (`ToResponse`, made public), `CustomerContracts.cs`, `Crm.Infrastructure/Tickets/TicketRepository.cs` (`Rows()` reads deleted customers), `Crm.Api/Endpoints/TicketsEndpoints.cs`.
3. Tests: `Crm.UnitTests/Tickets/TicketTestDoubles.cs`, `Crm.Api.IntegrationTests/Tickets/TicketsAuthorizationTests.cs` (endpoint list + policy map), `Tickets/TicketBodies.cs`.
4. Client: `pages/tickets/TicketDetailsPage.tsx` (+ test), `api/tickets.ts`, `api/customers.ts`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Tickets/TicketCustomerContextServiceTests.cs`: `Returns_TheCustomer_TheTotalAndTheLastFiveTickets` (AC 1, 2), `FollowsTheTicketsCurrentCustomer` (AC 4), `UnknownTicket_IsNotFound`, `DeletedCustomer_IsFlagged`.
- `Crm.Api.IntegrationTests/Tickets/TicketCustomerContextTests.cs`: customer with 7 tickets → total 7, five newest listed with statuses, current flagged; contacts present; another customer's tickets excluded; 404 for an unknown ticket; Agent allowed; user without `customers.view` gets 403.
- `TicketsAuthorizationTests`: endpoint listed (policies `tickets.view` + `customers.view`).

### 2 — Application / Infrastructure / Api

- `Crm.Application/Tickets/TicketCustomerContext.cs`: `CustomerTicketSummary`, `TicketCustomerContextResponse`, `ICustomerContextRepository` (`FindAsync(ticketId)` → `CustomerContextData?` = ticket, customer (deleted included), total, last 5 tickets), `ITicketCustomerContextService` + service (maps with `CustomerService.ToResponse`).
- `Crm.Infrastructure/Tickets/CustomerContextRepository.cs`; DI; endpoint `GET /api/tickets/{id:guid}/customer-context` (`tickets.view` + `customers.view`).

---

## Frontend Tasks

- Tests first: `features/tickets/CustomerPanel.test.tsx` (name, contacts, total, last 5 with status, profile link, reloads for another customer), `api/tickets.test.ts` (`getTicketCustomerContext`), `TicketDetailsPage.test.tsx` (panel shown for users with `customers.view`).
- `api/tickets.ts`, `features/tickets/CustomerPanel.tsx` + hook, `TicketDetailsPage` layout, i18n `tickets.customerPanel.*` (en + ar).

---

## Edge Cases & Failure Modes

- Customer without contacts: "No contact details" text.
- Customer with fewer than 5 tickets: all shown.
- Deleted customer: shown with a "deleted" note; the profile link is hidden (its page would 404).
- Missing `customers.view`: the panel is not rendered (`<Can>`), the API answers 403.

## Test Plan

Unit (service), integration (endpoint + authorization map), client (panel, API, details page).

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`.
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: name, contacts and total ticket count.
- [ ] AC 2: last 5 tickets with status.
- [ ] AC 3: link to the full profile.
- [ ] AC 4: panel follows the ticket's customer.
- [ ] build / tests / lint green.
