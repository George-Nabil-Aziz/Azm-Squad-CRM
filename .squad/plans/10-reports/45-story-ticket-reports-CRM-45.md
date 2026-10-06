# Story 45 — Ticket reports (Story: CRM-45)

## Prerequisites

- Merged / earlier on this branch: CRM-7 (`Permissions.ReportsView`), tickets + categories (CRM-12..14), CRM-34 (audit — unrelated), the "Reports" sidebar item (`navigation.ts`, `ComingSoonPage`).
- Branch `feature/phase2-group-r`. Reference only: CRM 01 `specs/40-ticket-reports/`.
- New package: none. No migration (read-only queries; an index on `Tickets.CreatedAt` already exists).
- **Creates the shared report shell** reused by CRM-46..49 (see [00-overview.md](00-overview.md)).

## Story Goal

A manager sees ticket volume by status, category, channel and priority (and per day) for a date range, can filter it, and can export it.

1. **AC 1 — counts match the database.** `GET /api/reports/tickets?from=&to=` counts tickets with `CreatedAt` in `[from 00:00, to+1 day 00:00)` UTC. Aggregates are computed in SQL (`GroupBy` projections). Response: `total`, `byStatus`, `byChannel`, `byPriority` (every value listed, zeros included), `byCategory` (categories with tickets + the uncategorized bucket), `byDay` (every day of the range, zero-filled). Default range: the last 30 days up to today (UTC).
2. **AC 2 — filters** `status`, `categoryId`, `channel`, `priority` (all optional, combinable; they narrow every aggregate). Unknown values, `from > to`, or a range over 366 days → 400 with the field name.
3. **AC 3 — export.** `GET /api/reports/tickets/export?...&format=csv|xlsx` (same filters) returns the report as a flat table (`Report, Value, Count`): CSV (UTF-8 + BOM, so Excel shows Arabic correctly) or a real `.xlsx` (hand-written OOXML in `System.IO.Compression`, numbers as numeric cells). `Content-Disposition` file name `ticket-report-<from>_<to>.<ext>`. Unknown format → 400.
4. **AC 4 — 403 for an Agent.** Both endpoints: `RequireAuthorization(Permissions.ReportsView)` (SuperAdmin, Admin, Supervisor); Agent 403, anonymous 401.
5. **Client:** `/reports` becomes the reports area (sub navigation, `/reports/tickets` is the first page): date range + status/category/channel/priority filters, summary cards, tables per breakdown, "Export CSV" / "Export Excel" buttons (authorized download through `apiGetBlob` + `saveFile`).

**Decisions**

- Date inputs are `DateOnly` (`yyyy-MM-dd`), interpreted in UTC (CLAUDE.md: dates in UTC). The business time zone of CRM-35 is not used for report days (documented limitation).
- One `ReportsRepository` (Infrastructure) for all reports; services in Application stay free of EF. Zero-filling and parsing live in the service (unit-tested with a fake repository).
- `ReportExporter` writes `ReportTable` (title, columns, rows of `object?`) — reused by 47.

## Tasks (tests first)

**T1 — Tests (Red):** unit `ReportRangeResolverTests` (defaults, inclusive end day, from>to, >366 days), `ReportExporterTests` (CSV BOM + escaping of comma/quote/newline + Arabic; xlsx is a zip with `xl/worksheets/sheet1.xml` holding header text and numeric cells; unknown format), `TicketReportServiceTests` (zero-filled days/status/priority/channel, filter parsing → 400 fields, passes UTC range to the repository, export table rows); integration `TicketReportTests` (counts equal an independent DB query for a seeded mix; each filter; category bucket; default range; 400s; csv/xlsx download headers and content; Agent 403 / Supervisor 200 / Admin 200 / 401 for both endpoints); client `api/reports.test.ts`, `TicketReportPage.test.tsx`, nav + route tests (`App.layout`, `App.permissions`).

**T2 — Application:** `Reports/ReportRange.cs`, `ReportContracts.cs`, `ReportText.cs`, `ReportTable.cs`, `ReportExporter.cs`, `TicketReportService.cs`, `IReportsRepository`.

**T3 — Infrastructure/Api:** `Reports/ReportsRepository.cs` (`TicketCountsAsync`), DI, `ReportsEndpoints.cs`.

**T4 — Client:** `api/reports.ts`, `features/reports/*` (`ReportsLayout`, `ReportFilters`, `ExportButtons`, `useTicketReport`), `pages/reports/TicketReportPage.tsx`, routes, i18n en/ar.

**T5 — Verify.**

## Edge cases

- Empty range → all zeros, `byCategory` empty, every day listed with 0.
- Deactivated / soft-deleted categories still show by name (tickets keep them); a deleted category id never appears twice.
- Large ranges: one query per breakdown (5 `GROUP BY`s), no row loading.

## Out of scope

Scheduled / emailed reports, PDF, per-agent breakdown (story 47), SLA numbers (story 46).
