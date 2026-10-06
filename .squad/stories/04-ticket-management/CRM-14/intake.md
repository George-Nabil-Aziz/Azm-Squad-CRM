# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/04-ticket-management/CRM-14/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-14` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Ticket list & filters
```

---

## Description

```
As an agent, I want to list and filter tickets, so that I can quickly find the tickets I need to work on.
```

---

## Acceptance criteria

```
1. Filter by status, priority, category, assignee, and date range.
2. Search by ticket number or subject.
3. Results are paginated and sorted by newest by default.
4. Combining filters returns only tickets that match all of them.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..11 (merged to `main`), CRM-12 (categories & priorities) and CRM-13 (create ticket) — same branch, done before this story.
- **Depends on code areas or other stories:** `Ticket`, `TicketView`, `TicketRepository.Rows()` (member-init projection; customers read with `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])`), `Ticket.TryParseNumber`, `TicketValues` (status / priority names), `TicketService.ToResponse`, `ticketsQueryKey` and the Tickets page (CRM-13); `listTicketCategories({})` (CRM-12: inactive categories included in filters); `PagedResult<T>` + `PagingDefaults` + `PagingText` + `LikePattern` (CRM-6 / CRM-8); permission `tickets.view`.

## Extra notes (optional)

- Phase 1 order: … CRM-12 ✅, CRM-13 ✅, **CRM-14 (this)**, CRM-15 (details & replies), CRM-16 (assign), CRM-17 (status workflow), CRM-18 (history), SLA CRM-19..22, channels CRM-23..26.
- Assignment arrives with CRM-16, status changes with CRM-17: the filters must already work on those columns (tests may set them directly in the database).
- A ticket of a soft-deleted customer is still listed (CRM-8 AC 5).
- Tests first (TDD).

## Technical hints (optional)

- `GET /api/tickets` with query-string filters, `page` / `pageSize` (default 20, max 100), newest first.
- The assignee filter needs a list of staff users that agents may read (`/api/users` needs `users.manage`).
- Client: filter bar + paged table on the Tickets page; all strings in `ar` + `en`.

## Out of scope

- Ticket details page (CRM-15), assigning (CRM-16), changing status (CRM-17), saved filters / views, sorting by other columns, export, SLA columns (CRM-20).
