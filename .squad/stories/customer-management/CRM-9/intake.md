# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-management/CRM-9/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Customer management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-9` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Customer contact details
```

---

## Description

```
As an agent, I want to store multiple phones, emails, and WhatsApp numbers per customer, so that incoming messages can be matched to the right customer.
```

---

## Acceptance criteria

```
1. Adding a phone in E.164 format (e.g. +9665xxxxxxxx) is saved.
2. An invalid phone or email returns 400.
3. Only one primary contact per type is allowed; setting a new primary unsets the old one.
4. Lookup by phone or email returns the matching customer (used later by channels).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1, CRM-5, CRM-2, CRM-3, CRM-4, CRM-6, CRM-7, CRM-8 — all done and merged to `main`.
- **Depends on code areas or other stories:** the `Customer` aggregate with its primary `Email` / `Phone` columns, `CustomerService` / `ICustomerRepository` / `CustomerRepository`, `CustomerConfiguration`, `CustomersEndpoints`, `CustomerText`, migration `AddCustomers` and the CRM-8 plan section "6 — How later stories build on this" (CRM-9 bullet: `CustomerContact` rows, copy the existing columns as primary contacts, E.164 rule replaces the loose phone rule, lookup = indexed exact match); the permission catalogue (`customers.view` reads, `customers.manage` writes — CRM-7); `PermissionPolicyTests` guards; client: the Customers page, `client/src/api/customers.ts`, `customer-form-schema.ts`, `<Can>`, `client/src/i18n/{en,ar}.json`.

## Extra notes (optional)

- Phase 1 execution order: foundation ✅, security-admin ✅, CRM-8 ✅, **CRM-9 (this)**, CRM-10 (interaction timeline), CRM-11 (notes & attachments), then tickets (CRM-12..18), SLA (CRM-19..22), email/WhatsApp channels (CRM-23..26).
- The channels (CRM-23..26) use this story's lookup to match an incoming email / WhatsApp message to its customer: design the lookup as an Application service method they can call directly, not only as an HTTP endpoint.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass. Contact rules (E.164, one primary per type) live in the Domain and are unit-tested without a database.
- Existing customers keep their data: the migration copies `Customers.Email` / `Customers.Phone` into the new contacts as primary contacts (data-migration SQL inside the migration, tested on a throw-away database).
- Permissions: reads `customers.view`, writes `customers.manage` (CRM-7 catalogue).

## Technical hints (optional)

- Phone numbers normalized to E.164 (Arabic-Indic digits and local Saudi formats like `050…` accepted on input); a phone-number library (e.g. libphonenumber-csharp) may be used in Application — Domain stays package-free.
- Routes under `/api/customers/{id}/contacts…` plus a lookup route; one migration.
- Client: a contacts dialog on the Customers page (list, add, make primary, remove); all strings in `ar` + `en`.

## Out of scope

- Customer details page and interaction timeline (CRM-10), notes and attachments (CRM-11).
- Receiving emails / WhatsApp messages and creating tickets from them (CRM-23..26) — only the lookup they will call.
- Editing a contact's value in place (remove + add), verifying numbers/emails (OTP, mail confirmation), merging duplicate customers, uniqueness of a number across customers, import/export.
