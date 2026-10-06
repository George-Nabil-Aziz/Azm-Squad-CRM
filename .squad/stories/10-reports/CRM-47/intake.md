# Story intake

- Folder: `.squad/stories/10-reports/CRM-47/intake.md`

---

## Feature

- **Feature name (display):** Reports
- **Feature slug (folder under `plans/`):** `10-reports`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-47`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Agent performance report
```

---

## Description

```
As a supervisor, I want an agent performance report, so that I can coach my team and balance the workload.
```

---

## Acceptance criteria

```
1. Per agent: tickets handled, average first response, average resolution, SLA %, and average CSAT.
2. A supervisor sees only their own team.
3. Supports a date range filter.
4. Can be exported to Excel/CSV.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-45 (shell, range, export), CRM-46 (SLA definitions), CRM-48 (`ICsatReadModel`; CRM-44 CSAT ratings are built on another branch).

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/42-agent-performance-reports/` (spec.md, contracts/api.md, tests/test-cases/42-agent-performance-reports.md).
- "Handled" = tickets assigned to the agent, created in the range. The system has no team concept: **AC 2 deviation** — a Supervisor (and Admin / SuperAdmin) sees all agents; documented in the plan.
- Average CSAT comes through `ICsatReadModel` (empty implementation until CRM-44 is merged: CSAT is `null`).

## Out of scope

- Teams / team membership, comparing periods, charts.
