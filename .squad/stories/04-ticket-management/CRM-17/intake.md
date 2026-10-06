# Story intake

- Folder: `.squad/stories/04-ticket-management/CRM-17/intake.md`

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-17`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

---

## Title

```
Ticket status workflow
```

---

## Description

```
As an agent, I want tickets to follow a clear status flow (New -> Open -> Pending -> Resolved -> Closed, with Reopen), so that progress is consistent and measurable.
```

---

## Acceptance criteria

```
1. A valid transition (Open -> Pending) succeeds.
2. An invalid transition (Closed -> Pending) returns 400.
3. Moving to Resolved sets ResolvedAt.
4. A Resolved ticket can be reopened to Open; ResolvedAt is cleared.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13 (`TicketStatus`), CRM-15 (a closed ticket takes no replies; its notice says "reopen"), CRM-16 (`ITicketHistoryRecorder`, `TicketHistoryField.Status`).
- **Used by:** CRM-18 (history shows status changes), SLA stories (`ResolvedAt`, stop timers on resolve).

## Extra notes (optional)

- Shared contract with parallel groups: `Ticket.ResolvedAt` is a nullable UTC `DateTime?` (exact name); this story sets it when a ticket becomes Resolved and clears it on reopen. The SLA group may add the same property; on merge keep one.
- Tests first (TDD).

## Technical hints (optional)

- Reference spec (read-only, older prototype): `D:/AzmSquad/9014 CRM/CRM 01/specs/08-update-ticket-status/` (`PATCH /api/tickets/{id}/status`, transition table) and `tests/test-cases/08-update-ticket-status.md`. Notion acceptance criteria and CLAUDE.md win where they differ (here ResolvedAt is cleared on reopen).
- Transitions (Domain, unit tested without a database): New -> Open; Open -> Pending, Resolved; Pending -> Open, Resolved; Resolved -> Closed, Open (reopen); Closed -> Open (reopen).

## Out of scope

- Automatic closing of resolved tickets, customer-triggered reopen from the portal, SLA timer pause / stop (SLA group), notifications.
