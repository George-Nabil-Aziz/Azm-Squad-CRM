# Story intake

- Folder: `.squad/stories/05-sla-automation/CRM-27/intake.md`

---

## Feature

- **Feature name (display):** SLA automation
- **Feature slug (folder under `plans/`):** `05-sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-27`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
SLA: automatic ticket assignment
```

---

## Description

```
As a supervisor, I want new tickets to be assigned automatically to an available agent (least open tickets), so that no ticket waits unassigned.
```

---

## Acceptance criteria

```
1. With auto-assign ON, a new ticket is assigned to the active agent with the fewest open tickets.
2. Inactive or off-duty agents are skipped.
3. With auto-assign OFF, the ticket stays unassigned.
4. The automatic assignment is recorded in the ticket history.
```

---

## Attachments

None.

---

## Dependencies

- CRM-13 (create ticket), CRM-16 (assignment), CRM-18 (history recorder), CRM-23..26 (channel tickets); CRM-28 (assignment notification) is built next.

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/23-automatic-assignment/` (rules based there; here simplified to least open tickets by the Notion AC).
- Record the assignment through `ITicketHistoryRecorder` (CRM-18).

## Out of scope

- Round-robin teams, skills / rules, working-hours calendars, re-assigning existing tickets.
