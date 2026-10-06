# Story intake

- Folder: `.squad/stories/09-customer-portal/CRM-42/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `09-customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-42`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer portal: track requests & history
```

---

## Description

```
As a customer, I want to track my tickets and see their history, so that I know what is happening with my requests.
```

---

## Acceptance criteria

```
1. A customer sees only their own tickets; opening another customer's ticket returns 404.
2. The customer sees status and public replies only, never internal notes.
3. The customer can reply on an open ticket.
4. The customer can reopen a resolved ticket within the allowed days.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-40, CRM-41; CRM-15 (messages), CRM-17 (status workflow), CRM-18 (history).
- **Depends on code areas or other stories:** `ITicketMessageRepository`, `TicketStatusRules`, `ITicketHistoryRecorder`.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/36-track-requests/`, `specs/37-view-history-portal/`.
- `GET /api/portal/tickets`, `GET /api/portal/tickets/{id}` (own only, else 404), `POST .../messages`, `POST .../reopen`. Reopen window `Portal:ReopenWindowDays` (default 7) counted from `ResolvedAt`; "open" for replies = New, Open or Pending.
- History shown to the customer = created + status changes (no assignee, priority or SLA data).

## Out of scope

- Editing or cancelling a ticket, attachments on replies.
