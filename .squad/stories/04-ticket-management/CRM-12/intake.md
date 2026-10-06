# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/04-ticket-management/CRM-12/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-12` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Ticket categories & priorities
```

---

## Description

```
As an admin, I want to manage ticket categories, with fixed priorities High / Mid / Low, so that tickets are classified consistently.
```

---

## Acceptance criteria

```
1. Admin creates a category -> it appears in the new-ticket form.
2. A duplicate category name returns 400.
3. An inactive category is not selectable for new tickets but stays on old tickets.
4. Priorities High, Mid, Low are available on every ticket.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..5 (foundation), CRM-6, CRM-7 (security-admin), CRM-8, CRM-9 (customers) — all done and merged to `main`.
- **Depends on code areas or other stories:** the permission catalogue (`categories.manage` for category admin: SuperAdmin + Admin; `tickets.view` / `tickets.manage` for every staff role — CRM-7), `PermissionPolicyTests` guards, the Application-service + Infrastructure-repository pattern of CRM-8 (`CustomerService` / `ICustomerRepository` / `CustomerRepository`), `IEntityTypeConfiguration<T>` in `Persistence/Configurations`, `<Feature>Text` server texts, the users / customers pages as UI precedent, `<Can>` / `RequirePermission`.

## Extra notes (optional)

- Phase 1 order: foundation ✅, security-admin ✅, CRM-8 ✅, CRM-9 ✅, CRM-10 / CRM-11 (built in parallel on another branch), **CRM-12 (this)**, CRM-13 (create ticket), CRM-14 (ticket list & filters), CRM-15..18 (details, assign, status workflow, history), SLA (CRM-19..22: due times per priority), channels (CRM-23..26).
- The new-ticket form (AC 1) arrives with CRM-13; this story delivers the category list it reads (only active categories) and the fixed priority list.
- Priorities are fixed (High, Mid, Low) — not editable; SLA (CRM-19) sets due times per priority.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code.

## Technical hints (optional)

- Categories: name (unique, case-insensitive), active flag; deactivate instead of delete so old tickets keep their category.
- Admin UI reachable only with `categories.manage`; all strings in `ar` + `en`.

## Out of scope

- Deleting categories, nested / sub-categories, per-category SLA or routing rules, translations of category names.
- The ticket entity and the new-ticket form (CRM-13), the ticket list (CRM-14).
