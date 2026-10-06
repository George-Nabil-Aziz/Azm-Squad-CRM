# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/04-ticket-management/CRM-13/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-13` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Create ticket
```

---

## Description

```
As an agent, I want to create a ticket for a customer, so that their request is tracked until it is solved.
```

---

## Acceptance criteria

```
1. Creating a ticket with customer, subject, description, category, and priority returns 201 with a ticket number (e.g. TKT-000001) and status New.
2. Missing customer or subject returns 400.
3. Ticket numbers are unique and sequential.
4. CreatedAt is stored in UTC.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..9 (merged to `main`), CRM-12 (ticket categories & priorities — same branch, done before this story).
- **Depends on code areas or other stories:** `TicketPriority`, `TicketCategory`, `TicketValues`, `ITicketCategoryRepository`, `/api/ticket-categories?activeOnly=true` and client `ticketPriorities` (CRM-12); the `Customer` aggregate and its soft-delete filter `CrmDbContext.SoftDeleteFilter` (CRM-8: tickets of soft-deleted customers must stay visible; read customers with `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])`); `ICurrentUser` (creator); permissions `tickets.view` / `tickets.manage` (CRM-7); client `listCustomers` for the customer picker.

## Extra notes (optional)

- Phase 1 order: … CRM-12 ✅, **CRM-13 (this)**, CRM-14 (ticket list & filters), CRM-15 (details & replies, `FirstResponseAt`), CRM-16 (assign), CRM-17 (status workflow New → Open → Pending → Resolved → Closed + reopen), CRM-18 (history), SLA CRM-19..22 (due times per priority), channels CRM-23..26 (tickets created from Email / WhatsApp / Portal).
- Design the `Ticket` entity so those stories extend it cleanly: status enum, priority enum High/Mid/Low, channel (Manual / Email / WhatsApp / Portal), sequential human number `TKT-000001`, `CreatedAt` UTC, `CustomerId`, `CategoryId`, nullable `AssigneeId`.
- CRM-10 (customer interaction timeline) is built in parallel on another branch: **do not** record the timeline entry here; the plan describes the follow-up to wire after CRM-10 merges.
- Tests first (TDD).

## Technical hints (optional)

- Number generation must work on SQL Server and on SQLite (integration tests): unique index on the number.
- Client: "New ticket" on the Tickets page (replaces the "coming soon" placeholder); category select shows only active categories; priorities High / Mid / Low.

## Out of scope

- Ticket list and filters (CRM-14), ticket details page / replies (CRM-15), assignment (CRM-16), status changes (CRM-17), history (CRM-18), SLA due times (CRM-19..22), attachments on tickets, creating tickets from channels (CRM-23..26).
