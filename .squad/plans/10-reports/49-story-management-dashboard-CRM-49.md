# Story 49 — Management dashboard (Story: CRM-49)

## Prerequisites

- Stories 45, 46, 47, 48 done on this branch (`ReportsRepository`, `ICsatReadModel`, `ReportsLayout`). Reference only: CRM 01 `specs/44-management-dashboards/`.
- New client component: shadcn `chart` (Recharts), added with `npx shadcn@4.21.2 add chart -y` (adds `client/src/components/ui/chart.tsx` and the `recharts` dependency). No migration, no server package.

## Story Goal

A live dashboard for managers: `GET /api/reports/dashboard` and the page `/reports/dashboard` (first item of the reports sub navigation; `/reports` redirects there).

1. **AC 1 — KPI cards:** `openTickets` (status not resolved / closed, now), `breachedToday` (tickets with a response or resolution due time earlier **today (UTC)** that is breached at "now"), `averageResponseMinutes` (average first response of tickets created in the last 30 days; null when none) and `averageCsat` (+ `csatCount`, last 30 days, through `ICsatReadModel`; null while CRM-44 is not wired).
2. **AC 2 — charts:** `ticketsPerDay` (last 14 days, every day listed) and `ticketsByChannel` (last 14 days, every channel listed): bar charts with shadcn `chart`.
3. **AC 3 — real-time refresh:** the client polls the endpoint every 30 s (`refetchInterval`, also on window focus); the cards show "Updated at <time>" from `generatedAt`. SignalR push is out of scope (decision: polling is enough for KPI granularity and needs no hub).
4. **AC 4 — access:** `reports.view` (Supervisor, Admin, SuperAdmin); Agent 403, anonymous 401. The sidebar "Reports" item and routes already use that permission.

**Decisions**

- Windows are fixed constants (`DashboardService.ChartDays = 14`, `AverageDays = 30`), "today" is the UTC day.
- Reuses repository methods of 45/46 (`TicketCountsAsync`, `SlaAggregatesAsync`) plus two new cheap queries (`OpenTicketsAsync`, `BreachedTodayAsync`); one request per refresh.

## Tasks (tests first)

**T1 — Tests (Red):** unit `DashboardServiceTests` (KPIs from fakes, zero-filled 14 days and all channels, averages null cases, windows and "today" passed to the repository, CSAT window); integration `DashboardTests` (open count equals an independent DB count; breached today with a ticket due earlier today; chart arrays; Agent 403 / Supervisor 200 / 401); client `api` test, `useDashboard` refetch test, `DashboardReportPage.test.tsx` (cards, charts present, "Updated at"), nav route test.

**T2 — Application/Infrastructure/Api:** `DashboardService`, repository methods, endpoint.

**T3 — Client:** `npx shadcn@4.21.2 add chart -y`, `getDashboard`, `useDashboard`, `pages/reports/DashboardReportPage.tsx`, route, i18n.

**T4 — Verify.**

## Out of scope

SignalR push, per-user layouts, drill-down from the charts, configurable windows.

## Deviations (as built)

- `theme.test.ts` (hex-color guard) skips the generated `components/ui/chart.tsx`: it only names Recharts' default `#ccc` / `#fff` attributes inside selectors to restyle them with theme variables.
- The reports area opens on the dashboard (`/reports` redirects to `/reports/dashboard`); ticket / SLA / agents / satisfaction reports stay in the sub navigation.
- Real time = 30 s polling (`refetchInterval`), no SignalR (as planned).
