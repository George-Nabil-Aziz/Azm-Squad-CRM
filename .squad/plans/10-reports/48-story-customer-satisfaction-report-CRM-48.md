# Story 48 — Customer satisfaction report (Story: CRM-48)

## Prerequisites

- Stories 45 (range, shell) and 46 done on this branch. Reference only: CRM 01 `specs/43-customer-satisfaction-reports/`.
- **CRM-44 (CSAT ratings) is built on another branch and is not available here.** This story reads ratings only through a new read-side interface `ICsatReadModel` (Application) with an empty implementation. **Wire to CRM-44 on merge:** replace the registration `services.AddScoped<ICsatReadModel, EmptyCsatReadModel>()` (in `Crm.Infrastructure/DependencyInjection.cs`) with an EF implementation over the CRM-44 rating table (and survey-sent count). Nothing else changes.
- No migration, no package. Execution order note: 48 is built before 47 because 47 reads CSAT through the same interface.

## Story Goal

`GET /api/reports/csat?from=&to=` (ratings given in the range, `RatedAt`):

1. **AC 1** — `averageRating`, `totalRatings`, `distribution` (counts for 1..5, all listed) and `byDay` (date, average, count; every day listed, average null when no rating).
2. **AC 2** — `byAgent` and `byCategory` groups: id, name, average, count (agents / categories without ratings are not listed; tickets without agent/category fall in a null bucket).
3. **AC 3** — `lowRatings`: ratings 1–2 with comment, ticket id + number (drill-down), agent, date; newest first.
4. **AC 4** — `surveysSent` and `responseRatePercent = totalRatings / surveysSent × 100` (null when no survey was sent).
5. `reports.view` (Agent 403). Client page `/reports/satisfaction`: cards, distribution and trend tables, groupings, low-rating list.

**Decisions**

- `ICsatReadModel.GetAsync(CsatFilter)` returns the ratings of the range plus the number of surveys sent; the service computes every aggregate (testable with fakes). When the real implementation exists it may filter in SQL; volume is bounded by resolved tickets.
- Averages rounded to 2 decimals; response rate to 1 decimal.

## Tasks (tests first)

**T1 — Tests (Red):** unit `CsatReportServiceTests` (distribution zeros, average, by day zero-fill, by agent / category incl. null bucket, low ratings only 1–2 newest first, response rate incl. null, range → UTC filter, empty read model); integration `CsatReportTests` (empty default implementation → zeros; replaced read model through `WithWebHostBuilder` → numbers on the wire; 400 range; Agent 403 / Supervisor 200 / 401); client `api` test + `CsatReportPage.test.tsx`.

**T2 — Application/Infrastructure/Api:** `ICsatReadModel`, `EmptyCsatReadModel`, `CsatReportService`, endpoint `/api/reports/csat`.

**T3 — Client:** `getCsatReport`, `CsatReportPage`, sub-nav, route, i18n.

**T4 — Verify.**

## Out of scope

Collecting ratings / surveys (CRM-44), export.
