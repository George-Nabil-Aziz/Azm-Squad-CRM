# Story intake

- Folder: `.squad/stories/10-reports/CRM-45/intake.md`

---

## Feature

- **Feature name (display):** Reports
- **Feature slug (folder under `plans/`):** `10-reports`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-45`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Ticket reports
```

---

## Description

```
As a manager, I want ticket volume reports by status, category, channel, and priority over a date range, so that I understand the support workload.
```

---

## Acceptance criteria

```
1. Counts match the database for the selected date range.
2. Reports can be filtered by status, category, channel, and priority.
3. Reports can be exported to Excel/CSV.
4. An Agent opening reports gets 403.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-7 (`reports.view`), CRM-13/14 (tickets), CRM-12 (categories). First story of the reports feature: it creates the shared range / export / client report shell that CRM-46..49 reuse.

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/40-ticket-reports/` (spec.md, contracts/api.md, tests/test-cases/40-ticket-reports.md).
- Aggregate in the database (EF `GroupBy` projections), date range in UTC, permission `reports.view` (SuperAdmin, Admin, Supervisor). Excel export = real `.xlsx` written with `System.IO.Compression` (no new package); CSV = UTF-8 with BOM so Excel opens Arabic text.

## Out of scope

- Scheduled / emailed reports, saved report definitions, PDF export.
