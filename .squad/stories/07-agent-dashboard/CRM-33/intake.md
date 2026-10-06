# Story intake

- Folder: `.squad/stories/07-agent-dashboard/CRM-33/intake.md`

---

## Feature

- **Feature name (display):** Agent dashboard
- **Feature slug (folder under `plans/`):** `07-agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-33`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Team collaboration (internal notes & mentions)
```

---

## Description

```
As an agent, I want to @mention colleagues in internal notes, so that I can get help on a ticket without leaving the CRM.
```

---

## Acceptance criteria

```
1. An @mention in an internal note notifies the mentioned user.
2. The notification opens the ticket directly.
3. Mentioning an inactive user sends no notification.
4. Internal notes and mentions are never visible to the customer.
```

---

## Attachments

None.

---

## Dependencies

- CRM-15 (internal notes, `TicketMessageService`), CRM-28 (`INotificationDispatcher`, `Mention` type, bell links to the ticket).

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/21-team-collaboration/`.
- The client sends the ids of the mentioned users (`mentionedUserIds`) next to the note text; only internal notes notify.

## Out of scope

- Mentions in public replies (ignored), mention parsing from free text, groups / role mentions, storing mentions as separate records.
