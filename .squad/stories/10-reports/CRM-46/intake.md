# Story intake

- Folder: `.squad/stories/10-reports/CRM-46/intake.md`

---

## Feature

- **Feature name (display):** Reports
- **Feature slug (folder under `plans/`):** `10-reports`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-46`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
SLA performance report
```

---

## Description

```
As a manager, I want an SLA performance report, so that I know how well we meet our response and resolution targets.
```

---

## Acceptance criteria

```
1. Shows % of tickets meeting response SLA and resolution SLA per priority.
2. Lists breached tickets with drill-down to each ticket.
3. Shows average first response time and average resolution time.
4. Supports a date range filter.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-45 (report range, shell, `ReportsRepository`), CRM-19..22 (SLA due times, `FirstResponseAt`, `ResolvedAt`, breach flags), CRM-35 (business-hours due times are already stored on the tickets).

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/41-sla-performance-reports/` (spec.md, contracts/api.md, tests/test-cases/41-sla-performance-reports.md).
- Same breach rule as `Ticket.IsResponseBreachedAt` / `IsResolutionBreachedAt`; computed in SQL with conditional counts. Permission `reports.view`.

## Out of scope

- Export of this report, per-agent SLA (story 47), changing SLA targets.
