# Story intake

- Folder: `.squad/stories/05-sla-automation/CRM-28/intake.md`

---

## Feature

- **Feature name (display):** SLA automation
- **Feature slug (folder under `plans/`):** `05-sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-28`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
SLA: alerts & notifications
```

---

## Description

```
As an agent or supervisor, I want in-app and email notifications for new assignments, SLA warnings, and breaches, so that I act on time.
```

---

## Acceptance criteria

```
1. Assigning a ticket notifies the assignee in-app in real time (SignalR).
2. SLA warning (80%) and breach send an in-app notification and an email.
3. Notifications can be marked as read and the unread count is shown.
4. The same event never notifies the same user twice.
```

---

## Attachments

None.

---

## Dependencies

- CRM-22 (Notification entity, ISlaNotifier, SlaMonitorJob), CRM-27 (auto-assignment), CRM-16 (assignment), CRM-23 (SMTP / ISmtpTransport).

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/25-alerts-notifications/`.
- SignalR hub under `/hubs/notifications`, JWT via `access_token` query string. Replace `LoggingSlaNotifier`.

## Out of scope

- Push / SMS, per-user notification preferences, notification digests.
