# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/03-customer-management/CRM-10/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Customer management
- **Feature slug (folder under `plans/`):** `03-customer-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-10` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: Mid`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Customer interaction history
```

---

## Description

```
As an agent, I want to see a timeline of all interactions with a customer (tickets, messages, notes), so that I have full context before replying.
```

---

## Acceptance criteria

```
1. The customer page shows a timeline ordered newest first.
2. Creating a ticket for the customer adds an entry to the timeline.
3. The timeline can be filtered by type (ticket, message, note).
4. The timeline is paginated.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..CRM-9 — all done and merged to `main`.
- **Depends on code areas or other stories:** the `Customer` aggregate, `CustomerService` / `ICustomerRepository`, `CustomersEndpoints`, `CustomerText`, `PagedResult<T>` + `PagingDefaults` + `PagingText`, `ICurrentUser`, the CRM-8 plan section "6 — How later stories build on this" (CRM-10/11 bullet: child entities with `CustomerId` FK `Restrict`, routes under `/api/customers/{id:guid}/…`, a details page at `customers/:id`) and the CRM-9 one (`useCustomer(id)`, contact changes are the place to raise timeline events); permissions `customers.view` (read) / `customers.manage` (write) from CRM-7.

## Extra notes (optional)

- Phase 1 order: foundation ✅, security-admin ✅, CRM-8 ✅, CRM-9 ✅, **CRM-10 (this)**, CRM-11 (notes & attachments), tickets (CRM-12..18), SLA (CRM-19..22), email/WhatsApp (CRM-23..26).
- Tickets and messages do not exist yet. Build a timeline mechanism the later stories plug into (CRM-11 notes, CRM-13 ticket created, CRM-15 / CRM-23..26 messages) and record what exists today (customer created / updated, contact added) so it is testable now.
- **AC 2 is completed by CRM-13** (ticket creation), with a test there; this story provides the mechanism and its contract.
- Tests first (TDD).

## Technical hints (optional)

- A details page `customers/:id` (the CRM-9 contacts dialog stays on the list).
- Timeline endpoint under `/api/customers/{id}/timeline` with type filter and paging.

## Out of scope

- Tickets, messages and their own pages (CRM-12..18, CRM-23..26); notes and attachments (CRM-11).
- Editing or deleting timeline entries; real-time updates (SignalR); an audit log of every field change.
