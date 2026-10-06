# Story intake

- Folder: `.squad/stories/02-security-admin/CRM-34/intake.md`

---

## Feature

- **Feature name (display):** Security & administration
- **Feature slug (folder under `plans/`):** `02-security-admin`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-34`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Audit logs
```

---

## Description

```
As a SuperAdmin, I want a log of sensitive actions (logins, user and role changes, SLA changes, deletes), so that every important change is traceable.
```

---

## Acceptance criteria

```
1. Each action is logged with user, action, entity, old/new values, IP, and time.
2. Failed login attempts are logged.
3. Logs can be filtered by user, action, and date.
4. Logs are read-only; only SuperAdmin and Admin can view them, others get 403.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-2 (login), CRM-6 (users), CRM-7 (permissions), CRM-8 (customer delete), CRM-19 (SLA policy) — all on `main`.

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/47-audit-logs/` (spec.md, contracts/api.md, tests/test-cases/47-audit-logs.md).
- New permission `audit.view` (SuperAdmin + Admin). No write endpoints: the log is append-only from the services.

## Out of scope

- Logging every ticket/customer edit (only the sensitive actions listed in the description), log retention/export.
