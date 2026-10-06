# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/03-customer-management/CRM-8/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Customer management
- **Feature slug (folder under `plans/`):** `03-customer-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-8` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Customer profiles (CRUD)
```

---

## Description

```
As an agent, I want to create, view, edit, and remove customer profiles, so that every ticket is linked to a known customer.
```

---

## Acceptance criteria

```
1. Creating a customer with a name returns 201.
2. Creating a customer without a name returns 400.
3. Customers list is paginated and searchable by name, phone, or email.
4. Editing a customer updates the profile.
5. Deleting is a soft delete: the customer disappears from the list but its tickets remain.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1, CRM-5, CRM-2, CRM-3, CRM-4, CRM-6, CRM-7 — all done and merged to `main`.
- **Depends on code areas or other stories:** `CrmDbContext` + migrations (CRM-2/CRM-6), `TimeProvider` registration (CRM-2), `PagedResult<T>` / `PagingDefaults` and the `EF.Functions.Like` search pattern of `UserService` (CRM-6), Application exceptions + `ValidateOrThrowAsync` (CRM-5), `LocalizedText` / `<Feature>Text` classes (CRM-4), the permission catalogue `Permissions.CustomersView` / `Permissions.CustomersManage` and the guard tests `EveryProtectedApiEndpoint_RequiresAKnownPermission` / `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` (CRM-7); client: the users page as the UI precedent (CRM-6), `<Can>` / `RequirePermission` and the `customers` route inside `RequirePermission` (CRM-7), `ComingSoonPage` (CRM-3), `client/src/i18n/{en,ar}.json` (CRM-4).

## Extra notes (optional)

- Phase 1 execution order: foundation (CRM-1, 5, 2, 3, 4) ✅, security-admin (CRM-6, CRM-7) ✅, **CRM-8 (this)**, CRM-9 (customer contact details: multiple phones/emails/WhatsApp, E.164, primary per type, lookup by phone/email), CRM-10 (interaction timeline), CRM-11 (notes & attachments), then tickets (CRM-12..18, every ticket belongs to a customer), SLA (CRM-19..22), email/WhatsApp (CRM-23..26).
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.
- First real Domain entity (`Customer`): design it so CRM-9..11 and tickets can extend it. Domain logic unit-tested without a database.
- Soft delete via `IsDeleted` + EF global query filter (CLAUDE.md); created/updated timestamps in UTC through the injected `TimeProvider`.
- Phone / email exist before CRM-9: the plan decides how (e.g. one primary phone and email on the customer now, generalized by CRM-9) and states the migration path.
- Permissions: reads `customers.view`, writes `customers.manage` (CRM-7 catalogue).

## Technical hints (optional)

- API under `/api/customers` (minimal-API endpoint class, route group, `RequireAuthorization(Permissions.X)`), list with `search`, `page`, `pageSize` → `PagedResult<CustomerResponse>`.
- EF configuration + new migration in `Crm.Infrastructure/Persistence/Migrations`.
- Client: Customers page (table, search, create/edit dialog, delete confirmation) replacing the `ComingSoonPage` route for `/customers`; all strings in `ar` + `en`.

## Out of scope

- Multiple contacts per customer, E.164 normalization, WhatsApp numbers, lookup by phone/email (CRM-9).
- Interaction timeline (CRM-10), notes and attachments (CRM-11), customer details page.
- Tickets and the ticket ↔ customer link (CRM-12..18); restoring (undeleting) customers; merging duplicates; import/export.
