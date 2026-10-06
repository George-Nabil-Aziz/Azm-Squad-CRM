# Story 21 — SLA breach detection (Hangfire job) (Story: CRM-21)

## Prerequisites

- Story 20 completed: [20-story-sla-timers-CRM-20.md](20-story-sla-timers-CRM-20.md) — `Ticket.ResponseDueAt` / `ResolutionDueAt` (indexed), `FirstResponseAt` / `ResolvedAt` (nullable; **set by CRM-15 / CRM-17** through `MarkFirstResponse` / `MarkResolved` / `Reopen`).
- CLAUDE.md (binding): Hangfire is **not started in `Testing`**; recurring jobs are plain classes whose method tests call directly; a fake `TimeProvider` (`CrmApiFactory.Time`) controls time. Hangfire storage = SQL Server; missing configuration must not crash startup.
- New packages (Crm.Infrastructure): **Hangfire.Core 1.8.25**, **Hangfire.NetCore 1.8.25**, **Hangfire.SqlServer 1.8.25** (netstandard2.0 / netcoreapp3.0 builds, run on .NET 10) and **Newtonsoft.Json 13.0.4** (lifts Hangfire.Core's `Newtonsoft.Json >= 11.0.1` above the vulnerable range, so `dotnet build` stays warning-free — NU1903). One migration: **`AddSlaBreaches`**.

---

## Story Goal

A job running every minute marks late tickets and keeps one breach record per ticket and kind.

1. A ticket whose `ResponseDueAt` has passed **without** `FirstResponseAt` (or whose first response came after `ResponseDueAt`) gets `ResponseBreached = true` and one `TicketSlaEvent { Type = ResponseBreached, DueAt, OccurredAt }` (AC 1).
2. A ticket with `FirstResponseAt <= ResponseDueAt` is never response-breached (AC 2).
3. A ticket resolved after `ResolutionDueAt` (or still unresolved after it) gets `ResolutionBreached = true` + one `ResolutionBreached` event (AC 3).
4. Running the job twice adds nothing the second time: candidates exclude already-flagged tickets, and a unique index on `(TicketId, Type, Level)` guards concurrent runs (AC 4).
5. Hangfire runs `SlaMonitorJob.RunAsync` as recurring job **`sla-monitor`** with cron `* * * * *` (`Cron.Minutely()`) (AC 5).
6. `TicketResponse` gains `responseBreached` / `resolutionBreached`; the ticket list SLA badges already show "overdue" (CRM-20).

**Decisions**

- **Breach rules in Domain** (`Ticket.IsResponseBreachedAt(now)`, `IsResolutionBreachedAt(now)`, `MarkResponseBreached()` / `MarkResolutionBreached()` returning "newly marked"), unit tested without a database.
- **Breach record = `Crm.Domain.Sla.TicketSlaEvent`** (`Id`, `TicketId`, `Type` = `SlaEventType { ResponseBreached, ResolutionBreached }`, `Level` (0 here; CRM-22 escalation levels), `DueAt?`, `OccurredAt`). It is the SLA history of a ticket: CRM-22 adds `ResponseWarning` / `Escalated`, CRM-18 can show these rows in the ticket history.
- **Job in Application** (`Crm.Application/Sla/SlaMonitorJob.cs`, no Hangfire reference; registered scoped). Hangfire wiring in **Infrastructure** (`Crm.Infrastructure/Jobs/JobsExtensions.cs`: `AddCrmJobs(IConfiguration)` + `RecurringJobs.Register(IRecurringJobManager)`), called from `Program.cs` **only when the environment is not `Testing`**, `Jobs:Enabled` is not `false` and `ConnectionStrings:Crm` is set; otherwise a log warning, no Hangfire. Registering the recurring job is wrapped in try/catch (database down → warning, the API still starts).
- Storage: `UseSqlServerStorage(() => new SqlConnection(cs))` (Microsoft.Data.SqlClient from EF Core; no System.Data.SqlClient), schema `HangFire` created by Hangfire (`PrepareSchemaIfNecessary`). No dashboard (would need auth; later story).
- Candidate query (`ITicketSlaRepository.ListBreachCandidatesAsync(now)`, tracked): `(!ResponseBreached && ResponseDueAt != null && ((FirstResponseAt == null && ResponseDueAt <= now) || FirstResponseAt > ResponseDueAt)) || (!ResolutionBreached && ResolutionDueAt != null && ((ResolvedAt == null && ResolutionDueAt <= now) || ResolvedAt > ResolutionDueAt))`, batch of 500 per run.
- A concurrent run that loses the unique index race → `DbUpdateException` caught by the repository → the run ends quietly (next minute retries; flags make it a no-op).

**Not in scope:** warnings / escalation / notifications (CRM-22), Hangfire dashboard, pausing SLA.

---

## Context — Read These Files First

1. `CLAUDE.md` (Integration tests, Hangfire); intake `.squad/stories/05-sla-automation/CRM-21/intake.md`.
2. `server/src/Crm.Domain/Tickets/Ticket.cs` (CRM-20 SLA members), `server/src/Crm.Domain/Sla/SlaPolicy.cs`.
3. `server/src/Crm.Application/Tickets/TicketService.cs` (`ToResponse`), `TicketContracts.cs`.
4. `server/src/Crm.Infrastructure/DependencyInjection.cs`, `Persistence/CrmDbContext.cs`, `Tickets/TicketRepository.cs`; `server/src/Crm.Api/Program.cs`.
5. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` (`Time`), `Sla/TicketSlaTimersTests.cs` (ticket creation helpers).

---

## Backend Tasks

### 1 — Unit tests first (Red)

- `Crm.UnitTests/Sla/TicketBreachTests.cs` — `NoReplyAfterResponseDue_IsResponseBreached` (AC 1), `ReplyWithinResponseTime_IsNotBreached` (AC 2), `LateReply_IsResponseBreached`, `ResolvedAfterResolutionDue_IsResolutionBreached` (AC 3), `ResolvedInTime_IsNotBreached`, `MarkBreached_Twice_ReturnsFalseTheSecondTime` (AC 4), `NoDueTimes_NeverBreached`.
- `Crm.UnitTests/Sla/SlaMonitorJobTests.cs` (fake `ITicketSlaRepository`) — `Run_MarksBreachesAndAddsOneEventPerKind`, `Run_Twice_AddsNoDuplicateEvents` (AC 4), `Run_UsesTheClock`.

### 2 — Domain + Application (Green)

- `Crm.Domain/Sla/SlaEventType.cs`, `TicketSlaEvent.cs` (`Create(ticketId, type, level, dueAt, utcNow)`).
- `Ticket`: `ResponseBreached`, `ResolutionBreached` + rule methods.
- `Crm.Application/Sla/ITicketSlaRepository.cs` (`ListBreachCandidatesAsync`, `AddEvent`, `SaveChangesAsync`), `SlaMonitorJob.cs` (`Task<SlaMonitorResult> RunAsync(CancellationToken)`; result counts for logs/tests), DI `AddScoped<SlaMonitorJob>()`.
- `TicketResponse` + `ResponseBreached`, `ResolutionBreached`.

### 3 — Integration tests (Red → Green)

- `Crm.Api.IntegrationTests/Sla/SlaBreachDetectionTests.cs` — create a High ticket via the API; advance `factory.Time` past the response time; resolve `SlaMonitorJob` from a scope and `RunAsync` → `GET /api/tickets/{id}` shows `responseBreached: true`, one `ResponseBreached` row (AC 1); a ticket replied in time (set `FirstResponseAt` through the Domain + `CrmDbContext`, since CRM-15 is not built) stays unbreached (AC 2); resolved late (`MarkResolved` via `CrmDbContext`) → `resolutionBreached` (AC 3); run twice → still one row per kind (AC 4).
- `Crm.Api.IntegrationTests/Sla/SlaJobScheduleTests.cs` — `RecurringJobs.Register` with a capturing `IRecurringJobManager` registers `sla-monitor`, cron `* * * * *`, type `SlaMonitorJob`, method `RunAsync` (AC 5); `Testing` host has **no** `IRecurringJobManager` / Hangfire server registered.

### 4 — Infrastructure + Api

- `Persistence/Configurations/TicketSlaEventConfiguration.cs` (table `TicketSlaEvents`, enum as string, FK `TicketId` Restrict, unique `(TicketId, Type, Level)`), `DbSet<TicketSlaEvent>`.
- `Crm.Infrastructure/Sla/TicketSlaRepository.cs`.
- `Crm.Infrastructure/Jobs/JobsExtensions.cs`, `RecurringJobs.cs`; `Program.cs`: `if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddCrmJobs(builder.Configuration);` and after `Build()` `app.Services.RegisterCrmRecurringJobs()`.
- Migration **`AddSlaBreaches`** (two bool columns on `Tickets` + table `TicketSlaEvents`).

---

## Frontend Tasks

- `api/tickets.ts`: `responseBreached`, `resolutionBreached` on `Ticket`. The list's SLA badges already show "overdue" from the due times; a breached ticket whose target was met late shows "met late" (CRM-20). No new UI text.

---

## Edge Cases & Failure Modes

- Hangfire DB unreachable at startup → warning logged, API runs, job not scheduled until next start.
- `Jobs:Enabled=false` or no connection string → no Hangfire (useful for local runs without SQL Server).
- Ticket reopened (CRM-17 `Reopen`) → `ResolvedAt` cleared; `ResolutionBreached` stays true (history is not rewritten).
- Due time exactly now → breached (`<= now`, matches the client "overdue from the due time on").
- Two job runs overlap (several servers) → unique index; the loser's save fails and is logged; flags prevent repeats.

## Test Plan

Unit (`TicketBreachTests`, `SlaMonitorJobTests`), integration (`SlaBreachDetectionTests`, `SlaJobScheduleTests`), existing suites unchanged. Client: type only (build + existing tests).

## Verification Steps

`cd server && dotnet build` (0 warnings) `&& dotnet test`; `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1–3 breach flags + events (unit + integration).
- [ ] AC 4 idempotent (second run adds nothing; unique index).
- [ ] AC 5 recurring job `sla-monitor` every minute; not started in `Testing`; missing config does not crash.
- [ ] build / tests / lint green.

## How later stories build on this (write nothing here; for later planners)

- **CRM-22:** extend `SlaMonitorJob.RunAsync` (same recurring job) with warnings and escalation; add `SlaEventType.ResponseWarning` / `Escalated` (levels 1, 2) to `TicketSlaEvents`.
- **CRM-18 (history):** show `TicketSlaEvents` rows in the ticket history.
- **CRM-15 / CRM-17:** setting `FirstResponseAt` / `ResolvedAt` is all the job needs.

> **Deviation:** after `git merge main` (CRM-15) the migrations `AddSlaPolicies`, `AddTicketSlaTimers`, `AddSlaBreaches` and `AddSlaEscalation` were replaced by ONE regenerated migration **`AddSla`** on top of main's `AddTicketMessages` (SLA policies + seed, ticket SLA columns, `TicketSlaEvents`, `Notifications`). `Ticket.FirstResponseAt` is the single field set by CRM-15's `RecordAgentReply`, which now calls `MarkFirstResponse`.
