# Story intake

- Folder: `.squad/stories/04-ticket-management/CRM-16/intake.md`

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-16`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

---

## Title

```
Assign ticket to agent
```

---

## Description

```
As a supervisor, I want to assign tickets to agents, so that every ticket has a clear owner.
```

---

## Acceptance criteria

```
1. Supervisor assigns a ticket -> assignee is updated and a history entry is recorded.
2. An agent without the assign permission assigning to someone else gets 403.
3. Assigning to an inactive user returns 400.
4. The assigned agent sees the ticket in their list.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13 (ticket), CRM-14 (list filter by assignee, `GET /api/tickets/assignees`), CRM-15 (details page, `ITicketRepository.FindAsync`), permissions CRM-7 (`tickets.assign`).
- **Used by:** CRM-17 and CRM-18 (history), SLA escalation / automatic assignment (write through `ITicketHistoryRecorder`).

## Extra notes (optional)

- Shared contract with parallel groups: all history entries (CRM-16/17, later SLA escalation) are written through one application interface `ITicketHistoryRecorder`. This story introduces it together with the history entry storage; CRM-18 adds the read API and the History tab.
- Supervisor and Admin have `tickets.assign`; the Agent role has only `tickets.manage` (an agent may take a ticket for themselves).
- Tests first (TDD).

## Technical hints (optional)

- Reference spec (read-only, older prototype): `D:/AzmSquad/9014 CRM/CRM 01/specs/07-assign-tickets/` (spec.md, contracts/api.md) and `tests/test-cases/07-assign-tickets.md`. Notion acceptance criteria and CLAUDE.md win where they differ.

## Out of scope

- Automatic assignment (SLA group), bulk assignment, notifications to the new assignee, the history read API / tab (CRM-18).
