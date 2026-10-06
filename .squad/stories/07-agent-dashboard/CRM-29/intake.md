# Story intake

- Folder: `.squad/stories/07-agent-dashboard/CRM-29/intake.md`

---

## Feature

- **Feature name (display):** Agent dashboard
- **Feature slug (folder under `plans/`):** `07-agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-29`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
My assigned tickets
```

---

## Description

```
As an agent, I want a dashboard of my assigned tickets sorted by SLA urgency, so that I always work on the most urgent ticket first.
```

---

## Acceptance criteria

```
1. Shows only tickets assigned to me that are not Closed.
2. Sorted by the nearest SLA due time first.
3. Shows counters: open, pending, breached today.
4. Clicking a ticket opens its details.
```

---

## Attachments

None.

---

## Dependencies

- CRM-16 (assignment), CRM-20..22 (SLA due times, breaches, `TicketSlaEvents`), CRM-14 (ticket list / details), existing Dashboard page (CRM-1/5).

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/17-view-assigned-tickets/`.
- Extend `client/src/pages/dashboard/DashboardPage.tsx`; new endpoint `GET /api/tickets/mine` (`tickets.view`).

## Out of scope

- Team / supervisor dashboards, charts and reports (reports feature), per-user time zones for "today".
