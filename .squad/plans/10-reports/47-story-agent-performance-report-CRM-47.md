# Story 47 — Agent performance report (Story: CRM-47)

## Prerequisites

- Stories 45, 46, 48 done on this branch (range, export, SLA definitions, `ICsatReadModel`). Built after 48 because it reads CSAT through the same interface. Reference only: CRM 01 `specs/42-agent-performance-reports/`.
- No migration, no package.

## Story Goal

`GET /api/reports/agents?from=&to=` — one row per agent (user assigned to at least one ticket **created** in the range):

1. **AC 1** — `ticketsHandled` (assigned tickets created in the range), `averageFirstResponseMinutes`, `averageResolutionMinutes` (over tickets with that result), `slaPercent` = (response met + resolution met) / (met + breached of both targets) × 100 with the story-46 rules (null when nothing decided), `averageCsat` + `csatCount` from `ICsatReadModel` ratings of that agent (null while CRM-44 is not wired). Sorted by tickets handled, then name.
2. **AC 2 (deviation)** — "a supervisor sees only their own team": the system has **no team concept** (no team table, no supervisor→agent link). Scope = **all agents for Supervisor, Admin and SuperAdmin**. Recorded here; a future team story only needs to filter the repository query by team members.
3. **AC 3** — date range as in story 45 (default last 30 days, 400 on bad range).
4. **AC 4** — `GET /api/reports/agents/export?...&format=csv|xlsx` writes the same table (`ReportExporter`).
5. `reports.view` on both (Agent 403). Client page `/reports/agents` (table + export buttons).

**Decisions**

- Counts per agent in SQL (`GROUP BY AssigneeId`, conditional counts); minute sums streamed from two datetime columns (as story 46, SQLite cannot translate time spans).
- Ratings are matched to agents by `AgentId`; ratings of agents without tickets in the range are not listed.

## Tasks (tests first)

**T1 — Tests (Red):** unit `AgentReportServiceTests` (averages, SLA % combined, null cases, CSAT merge, ordering, export rows/columns, range → UTC + now); integration `AgentReportTests` (seeded tickets of two agents with ExecuteUpdate; counts, averages and SLA % equal expectations; unassigned tickets ignored; range; CSV/XLSX export; 400s; Agent 403 / Supervisor 200 / 401); client `api` + `AgentReportPage.test.tsx`.

**T2 — Application/Infrastructure/Api:** `AgentReportService`, repository `AgentAggregatesAsync`, endpoints.

**T3 — Client:** `getAgentReport`, `exportAgentReport`, page, sub-nav, route, i18n.

**T4 — Verify.**

## Out of scope

Teams, workload balancing suggestions, comparing periods.
