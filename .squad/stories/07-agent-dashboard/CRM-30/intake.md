# Story intake

- Folder: `.squad/stories/07-agent-dashboard/CRM-30/intake.md`

---

## Feature

- **Feature name (display):** Agent dashboard
- **Feature slug (folder under `plans/`):** `07-agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-30`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer info side panel
```

---

## Description

```
As an agent, I want to see the customer's information next to the ticket, so that I have context without leaving the screen.
```

---

## Acceptance criteria

```
1. The ticket page shows customer name, contact details, and total ticket count.
2. The last 5 tickets of the customer are listed with their status.
3. A link opens the full customer profile.
4. The panel updates if the ticket's customer is changed.
```

---

## Attachments

None.

---

## Dependencies

- CRM-8/9 (customers, contacts), CRM-14 (ticket details page), CRM-29 (agent dashboard feature folder).

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/18-customer-info-context/`.
- New endpoint `GET /api/tickets/{id}/customer-context`; panel in `TicketDetailsPage`.

## Out of scope

- Editing the customer from the panel, changing a ticket's customer (no such endpoint exists; the panel simply follows the ticket's current customer).
