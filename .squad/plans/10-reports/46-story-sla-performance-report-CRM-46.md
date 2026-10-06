# Story 46 — SLA performance report (Story: CRM-46)

## Prerequisites

- Story 45 done on this branch (`ReportRange`, `ReportsRepository`, `ReportsEndpoints`, `ReportsLayout`, `BreakdownTable`, `DateRangeFields`).
- SLA data on tickets (CRM-19..22): `ResponseDueAt`, `ResolutionDueAt`, `FirstResponseAt`, `ResolvedAt` (due times already honour CRM-35 business hours). Reference only: CRM 01 `specs/41-sla-performance-reports/`.
- No migration, no package.

## Story Goal

1. **AC 1** — `GET /api/reports/sla?from=&to=` (tickets **created** in the range): per priority (high, mid, low) and overall: tickets, and for the **response** and the **resolution** target: `met`, `breached`, `pending` and `compliancePercent = met / (met + breached) × 100` (null when nothing was decided yet). Rules, identical to `Ticket.IsResponseBreachedAt` / `IsResolutionBreachedAt`: *met* = result time <= due; *breached* = result after due, or no result and due <= now; *pending* = no result and due in the future. Tickets without due times (older than SLA timers) are not counted.
2. **AC 3** — same response: `averageMinutes` per target (first response − created; resolved − created) over the tickets that have the result, per priority and overall (overall = weighted, not the mean of means).
3. **AC 2** — `GET /api/reports/sla/breaches?from=&to=&page=&pageSize=` lists breached tickets (response and/or resolution breached at "now"), newest first, as `PagedResult` with `ticketId`, `number`, `subject`, `priority`, `assigneeName`, `createdAt`, due/result times and both breach flags. The client links each row to `/tickets/{id}` (drill-down).
4. **AC 4** — `from`/`to` as in story 45 (default last 30 days; 400 on bad range).
5. `reports.view` on both endpoints (Agent 403). Client: new report page `/reports/sla` with a per-priority table, compliance badges and the breached-ticket list.

**Decisions**

- Aggregates in SQL: one `GROUP BY Priority` with conditional counts and sums; minutes via `(a - b).TotalMinutes` EF translation (verified on SQLite in tests; SQL Server translates the same expression to `DATEDIFF`).
- "Now" comes from the injected `TimeProvider` (tests control it).
- Compliance is rounded to one decimal; averages to one decimal minute.

## Tasks (tests first)

**T1 — Tests (Red):** unit `SlaReportServiceTests` (percentages incl. null, weighted overall, averages, zero priorities listed, UTC range + now to repository, breaches paging validation, `number` built from prefix + number); integration `SlaReportTests` (seeded mix of met / breached / pending tickets with ExecuteUpdate; numbers equal an independent in-memory calculation; per-priority; date range excludes tickets; breaches list content and paging; 400s; Agent 403 / Supervisor 200 / 401); client `api/reports.test.ts` additions, `SlaReportPage.test.tsx` (rows, percentages, breached link to the ticket, range filter).

**T2 — Application:** `SlaReportService`, contracts (`SlaReportResponse`, `SlaPriorityRow`, `SlaTargetStats`, `BreachedTicketResponse`), repository methods `SlaAggregatesAsync`, `BreachedTicketsAsync`.

**T3 — Infrastructure/Api:** `ReportsRepository` additions, endpoints.

**T4 — Client:** `getSlaReport`, `listSlaBreaches`, `SlaReportPage`, sub-nav link, route, i18n.

**T5 — Verify.**

## Edge cases

- A ticket answered exactly at the due time counts as met.
- Reopened tickets keep their last `ResolvedAt` semantics from CRM-17 (cleared on reopen → unresolved again).
- No tickets in range → all zeros, compliance null (shown as "–").

## Out of scope

Export, per-agent SLA (47), editing SLA policies.
