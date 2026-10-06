# 10-reports — plan overview

Entry point for the **reports** feature (Phase 2): management reports over tickets, SLA, agents and customer satisfaction, and the management dashboard. Stories execute in order by their `NN` prefix; `NN` is the story number (Notion order).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 45 | [45-story-ticket-reports-CRM-45.md](45-story-ticket-reports-CRM-45.md) | Ticket reports (+ Excel/CSV export) | CRM-45 | 07 (permissions), 13–14 (tickets) |
| 46 | [46-story-sla-performance-report-CRM-46.md](46-story-sla-performance-report-CRM-46.md) | SLA performance report | CRM-46 | 45, 19–22 (SLA) |
| 47 | [47-story-agent-performance-report-CRM-47.md](47-story-agent-performance-report-CRM-47.md) | Agent performance report | CRM-47 | 45, 46, 48 (`ICsatReadModel`) |
| 48 | [48-story-customer-satisfaction-report-CRM-48.md](48-story-customer-satisfaction-report-CRM-48.md) | Customer satisfaction report | CRM-48 | 45; CRM-44 (CSAT ratings, other branch) |
| 49 | [49-story-management-dashboard-CRM-49.md](49-story-management-dashboard-CRM-49.md) | Management dashboard | CRM-49 | 45, 46, 48 |

## Dependency notes

- **Shared contracts created by story 45** and reused by 46–49: `Crm.Application/Reports/` — `ReportRange` + `ReportRangeResolver` (`from`/`to` dates, UTC, default last 30 days, max 366 days, 400 on bad input), `ReportTable` + `ReportExporter` (CSV UTF-8 with BOM; real `.xlsx` via `System.IO.Compression`), `ReportText`, `IReportsRepository` (EF projections in `Crm.Infrastructure/Reports/ReportsRepository.cs`); API `Crm.Api/Endpoints/ReportsEndpoints.cs` under `/api/reports/*` with `RequireAuthorization(Permissions.ReportsView)` (SuperAdmin, Admin, Supervisor; Agent gets 403); client `api/reports.ts`, `features/reports/` (range filter, export button, report shell + sub navigation), route `/reports/*` replacing the "coming soon" page.
- **Aggregation happens in the database** (`GroupBy` + `Select` projections, no tickets loaded into memory); ranges filter `Tickets.CreatedAt` in UTC.
- **CSAT** (CRM-44) is built on another branch. Story 48 defines the read-side interface `ICsatReadModel` (Application) with an empty implementation registered now (`EmptyCsatReadModel`: no ratings); report logic is tested with fakes. **Wire to CRM-44 on merge:** replace the registration with an EF implementation over the CRM-44 rating table. Stories 47 and 49 read CSAT through the same interface.
- **Teams:** the system has no team concept; story 47's "supervisor sees only their own team" is implemented as all agents for Supervisor and above (documented deviation).
- **Management dashboard (49)** reads the aggregates of 45/46/48 and refreshes by polling (React Query `refetchInterval`); charts use shadcn `chart` (Recharts).
