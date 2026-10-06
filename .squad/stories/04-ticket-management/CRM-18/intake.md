# Story intake

- Folder: `.squad/stories/04-ticket-management/CRM-18/intake.md`

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-18`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: Mid`

---

## Title

```
Ticket history (audit trail)
```

---

## Description

```
As a supervisor, I want every change on a ticket recorded, so that I can see who changed what and when.
```

---

## Acceptance criteria

```
1. Changes to status, assignee, priority, and category are recorded with old value, new value, user, and time.
2. The ticket History tab shows entries in chronological order.
3. History entries cannot be edited or deleted through the API.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-16 (`TicketHistoryEntry`, `ITicketHistoryRecorder`, assignee entries), CRM-17 (status entries), CRM-20 (priority endpoint `PUT /api/tickets/{id}/priority`), CRM-22 (`SlaMonitorJob` writes `Escalated` rows to `TicketSlaEvents`).

## Extra notes (optional)

- Shared contract: all ticket changes are written through `ITicketHistoryRecorder`. This story adds the missing writers (priority, and a new category change endpoint, since no endpoint changed the category yet) and the read side.
- The SLA group's `Escalated` events (`TicketSlaEvents`) are shown in the history as "escalation" entries (no user, new value = the level).
- Tests first (TDD).

## Technical hints (optional)

- Reference spec (read-only, older prototype): `D:/AzmSquad/9014 CRM/CRM 01/specs/10-ticket-history/` (`GET /api/tickets/{id}/history`) and `tests/test-cases/10-ticket-history.md`. Notion acceptance criteria and CLAUDE.md win where they differ.

## Out of scope

- Filtering / paging the history, export, showing message or note events (the thread has them), SLA breach / warning events.
