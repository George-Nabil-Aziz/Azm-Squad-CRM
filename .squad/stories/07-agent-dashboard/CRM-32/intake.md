# Story intake

- Folder: `.squad/stories/07-agent-dashboard/CRM-32/intake.md`

---

## Feature

- **Feature name (display):** Agent dashboard
- **Feature slug (folder under `plans/`):** `07-agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-32`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Quick replies (canned responses)
```

---

## Description

```
As an agent, I want saved reply templates (quick replies) with placeholders, so that I answer common questions faster and consistently.
```

---

## Acceptance criteria

```
1. A quick reply can contain placeholders like {{customer.name}} and {{ticket.number}}.
2. Inserting it into a reply replaces placeholders with the ticket data.
3. Quick replies are personal or shared; editing a shared one without permission returns 403.
4. Quick replies are searchable by title or shortcut.
```

---

## Attachments

None.

---

## Dependencies

- CRM-15 (ticket replies / `TicketReplyForm`), CRM-7 permissions catalogue.

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/20-quick-replies/`.
- New permission `quick-replies.manage-shared` (Supervisor, Admin, SuperAdmin); using quick replies needs `tickets.manage`.

## Out of scope

- Rich text, attachments in templates, per-team sharing, usage statistics.
