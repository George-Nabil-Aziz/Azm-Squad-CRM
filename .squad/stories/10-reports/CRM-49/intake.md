# Story intake

- Folder: `.squad/stories/10-reports/CRM-49/intake.md`

---

## Feature

- **Feature name (display):** Reports
- **Feature slug (folder under `plans/`):** `10-reports`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-49`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Management dashboard
```

---

## Description

```
As a manager, I want a live management dashboard with key KPIs, so that I see the support status at a glance.
```

---

## Acceptance criteria

```
1. KPI cards: open tickets, breached today, average response time, average CSAT.
2. Charts: tickets per day and tickets by channel.
3. Data refreshes automatically in real time.
4. Only Supervisor, Admin, and SuperAdmin can open it; others get 403.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-45 (ticket counts per day / channel), CRM-46 (breach rule, average first response), CRM-48 (`ICsatReadModel`).

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/44-management-dashboards/` (spec.md, contracts/api.md, tests/test-cases/44-management-dashboards.md).
- One endpoint `GET /api/reports/dashboard`, permission `reports.view`. "Real time" = the client polls with React Query `refetchInterval` (SignalR push is out of scope). Charts: shadcn `chart` (Recharts), added with `npx shadcn@4.21.2 add chart -y`.

## Out of scope

- SignalR push, per-user dashboard layouts, drill-down from charts.
