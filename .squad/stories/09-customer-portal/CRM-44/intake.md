# Story intake

- Folder: `.squad/stories/09-customer-portal/CRM-44/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `09-customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-44`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer portal: submit feedback (CSAT)
```

---

## Description

```
As a customer, I want to rate the support I received after my ticket is resolved, so that the company can measure satisfaction (CSAT).
```

---

## Acceptance criteria

```
1. When a ticket is Resolved, the customer gets a survey (in the portal and by email link).
2. A rating 1-5 with an optional comment is saved once per ticket; a second submit returns 400.
3. A rating outside 1-5 returns 400.
4. The survey link expires after 7 days.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-17 (status workflow), CRM-40..42 (portal), CRM-23 (email).
- **Depends on code areas or other stories:** `TicketStatusService`, `IChannelSender`.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/39-submit-feedback/`.
- `TicketSurvey` (one per ticket, random token, `ExpiresAt` = issue + 7 days). Created and emailed when a ticket becomes Resolved; a ticket resolved again after a reopen re-issues the survey only while it is unanswered.
- Portal: `POST /api/portal/tickets/{id}/feedback` (signed in) and the anonymous email link `GET/POST /api/portal/surveys/{token}`.

## Out of scope

- CSAT reports and dashboards (reports feature), reminders.
