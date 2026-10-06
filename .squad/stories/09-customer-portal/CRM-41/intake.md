# Story intake

- Folder: `.squad/stories/09-customer-portal/CRM-41/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `09-customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-41`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer portal: submit ticket
```

---

## Description

```
As a customer, I want to submit a ticket from the portal, so that I can ask for help any time.
```

---

## Acceptance criteria

```
1. Submitting subject, description, category, and attachments creates a ticket with channel = Portal.
2. The customer sees the ticket number and receives a confirmation email.
3. Missing subject returns 400.
4. SLA timers start when the ticket is created.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-40 (portal login), CRM-13 (create ticket), CRM-20 (SLA timers), CRM-23 (email).
- **Depends on code areas or other stories:** `TicketService`, `IChannelSender`, `IFileStorage`, `AttachmentRules`.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/35-submit-tickets-portal/`.
- `POST /api/portal/tickets` (multipart: subject, description, categoryId, files). Creation goes through `TicketService` (new `CreateForCustomerAsync`) so numbering, SLA and timeline are shared.
- Attachments are stored per ticket (`TicketAttachment` + `IFileStorage`, same rules as customer attachments); staff list / download them under `/api/tickets/{id}/attachments`.
- Confirmation email carries `[TKT-n]` in the subject so replies thread.

## Out of scope

- Priority selection by the customer, drafts, portal ticket editing.
