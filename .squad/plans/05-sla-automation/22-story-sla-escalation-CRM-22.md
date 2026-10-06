# Story 22 — SLA escalation rules (Story: CRM-22)

## Prerequisites

- Stories 19–21 completed: [21-story-sla-breach-detection-CRM-21.md](21-story-sla-breach-detection-CRM-21.md) — `SlaMonitorJob` (recurring `sla-monitor`, every minute; plain class, tests call `RunAsync`), `TicketSlaEvent` with unique `(TicketId, Type, Level)`, breach rules on `Ticket`; [20-story-sla-timers-CRM-20.md](20-story-sla-timers-CRM-20.md) — `ApplySla` / `ChangePriority`, `FirstResponseAt` / `ResolvedAt` (**set by CRM-15 / CRM-17**).
- `Ticket.AssigneeId` exists (CRM-13 column; CRM-16 assigns). Roles from CRM-2/7: `Supervisor`.
- No new packages. One migration: **`AddSlaEscalation`**.

---

## Story Goal

Tickets warn their assignee before the response deadline and escalate to supervisors on a breach — once per level, never after resolution.

1. When **80 %** of the response window (`CreatedAt → ResponseDueAt`) has passed, the ticket has no first response, is not resolved and was not warned yet → `ResponseWarnedAt = now`, a `ResponseWarning` SLA event, a **notification for the assignee** (`Notification { RecipientUserId = AssigneeId }`; unassigned → to the `Supervisor` role) and `ISlaNotifier.NotifyAsync` (AC 1).
2. When the job detects a breach (CRM-21) on an **unresolved** ticket → `EscalationLevel += 1`, `EscalatedAt = now`, an `Escalated` SLA event with that level (**the history entry**), a notification for the `Supervisor` role and `ISlaNotifier.NotifyAsync` (AC 2). Level 1 = response breach, level 2 = resolution breach (if response was not breached, the resolution breach is level 1).
3. Each level is triggered at most once: the job only escalates when it newly marks a breach (CRM-21 flags), and the unique `(TicketId, Type, Level)` index rejects a repeat (AC 3). Warning likewise (`ResponseWarnedAt` set once).
4. A resolved ticket (`ResolvedAt != null`) gets no warning and no escalation, even if its resolution was late (breach is still recorded by CRM-21) (AC 4).
5. `TicketResponse` gains `escalationLevel`, `responseWarnedAt`; the ticket list shows an "Escalated (level n)" badge next to the SLA timers.

**Decisions**

- **Warning time stored on the ticket**: `ResponseWarningAt = CreatedAt + 0.8 × (ResponseDueAt − CreatedAt)` set by `ApplySla` / `ChangePriority` (so the job query is a plain column comparison). Tickets created before this migration have null and are never warned.
- **Rules in Domain**: `Ticket.TryWarnResponse(now)` (bool), `Ticket.Escalate(now)` (returns the new level, throws when resolved), `SlaPolicy.WarningFraction = 0.8`.
- **Notifications**: `Crm.Domain.Notifications.Notification` (`Id`, `RecipientUserId?`, `RecipientRole?`, `TicketId`, `Type` (`SlaWarning` / `SlaEscalation`), `Level`, `CreatedAt`, `ReadAt?`) — stored only; the in-app notifications UI is Phase 2. `Crm.Application/Sla/ISlaNotifier` (`NotifyAsync(SlaNotice, ct)`) with `LoggingSlaNotifier` in Infrastructure (logs; a later story sends e-mail / SignalR). Notifier failures are logged, never fail the job.
- **History entry** = the `Escalated` `TicketSlaEvent` row (CRM-18 will show SLA events in the ticket history). The warning is a `ResponseWarning` event too.
- **Escalation target** = role `Supervisor` (no team model yet); `RecipientRole = "Supervisor"`.
- Order inside one run: breaches first (CRM-21), then escalations for newly breached unresolved tickets, then warnings (a ticket already breached is not warned anymore).

**Not in scope:** notification UI / e-mail / SignalR, configurable thresholds, escalation chains beyond supervisors, de-escalation.

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/05-sla-automation/CRM-22/intake.md`.
2. `server/src/Crm.Domain/Tickets/Ticket.cs`, `server/src/Crm.Domain/Sla/*`.
3. `server/src/Crm.Application/Sla/SlaMonitorJob.cs`, `ITicketSlaRepository.cs`; `server/src/Crm.Application/Auth/Roles.cs`.
4. `server/src/Crm.Infrastructure/Sla/TicketSlaRepository.cs`, `DependencyInjection.cs`, `Persistence/CrmDbContext.cs`.
5. `server/tests/Crm.UnitTests/Sla/SlaMonitorJobTests.cs` + doubles; `server/tests/Crm.Api.IntegrationTests/Sla/SlaBreachDetectionTests.cs`.
6. Client: `client/src/features/sla/TicketSlaTimers.tsx`, `client/src/api/tickets.ts`.

---

## Backend Tasks

### 1 — Unit tests first (Red)

- `Crm.UnitTests/Sla/TicketEscalationTests.cs` — `WarningAt_Is80PercentOfTheResponseWindow`, `TryWarn_Before80Percent_False`, `TryWarn_After80Percent_TrueOnce` (AC 1/3), `TryWarn_WhenRespondedOrResolved_False` (AC 4), `Escalate_IncrementsTheLevel` (AC 2), `Escalate_WhenResolved_Throws` (AC 4).
- `Crm.UnitTests/Sla/SlaMonitorJobTests.cs` — `Run_At80Percent_WarnsTheAssignee_Once` (AC 1, 3), `Run_Unassigned_WarnsSupervisors`, `Run_OnBreach_EscalatesToSupervisor_AndAddsAHistoryEvent` (AC 2), `Run_Twice_NeverEscalatesTheSameLevelAgain` (AC 3), `Run_ResponseThenResolutionBreach_Levels1And2`, `Run_ResolvedTicket_NoWarningNoEscalation` (AC 4), `Run_NotifierFailure_DoesNotStopTheJob`.

### 2 — Domain + Application (Green)

- `Ticket`: `ResponseWarningAt`, `ResponseWarnedAt`, `EscalationLevel`, `EscalatedAt`; methods above; `ApplySla` / `ChangePriority` set `ResponseWarningAt`.
- `SlaEventType` + `ResponseWarning`, `Escalated`.
- `Crm.Domain/Notifications/Notification.cs`, `NotificationType.cs`.
- `Crm.Application/Sla/ISlaNotifier.cs` (+ `SlaNotice` record), `ITicketSlaRepository` + `ListWarningCandidatesAsync(now)`, `AddNotification`; `SlaMonitorJob` extended; `TicketResponse` + `EscalationLevel`, `ResponseWarnedAt`.

### 3 — Integration tests

- `Crm.Api.IntegrationTests/Sla/SlaEscalationTests.cs` — High ticket, assignee set via `CrmDbContext` (CRM-16 not built): clock to 80 % → run → one warning notification for the assignee (AC 1); clock past due → run → `escalationLevel: 1`, `Escalated` event, supervisor notification (AC 2); run again → nothing new (AC 3); resolved ticket (`MarkResolved`) → no escalation (AC 4).

### 4 — Infrastructure

- `NotificationConfiguration` (table `Notifications`, FK `TicketId` Restrict, FK `RecipientUserId` → `AspNetUsers` Restrict, index `(RecipientUserId, ReadAt)`); `LoggingSlaNotifier`; repository queries; registrations.
- Migration **`AddSlaEscalation`**.

---

## Frontend Tasks

- Tests first: `TicketSlaTimers.test.tsx` — "Escalated (level 1)" badge when `escalationLevel > 0`.
- `api/tickets.ts` + `escalationLevel`, `responseWarnedAt`; `TicketSlaTimers` shows the badge; i18n `sla.timer.escalated` (en + ar).

---

## Edge Cases & Failure Modes

- Priority change after a warning → `ResponseWarningAt` recalculated; `ResponseWarnedAt` stays (no second warning).
- Response breached and resolved later late → level 1 only (resolved tickets are not escalated).
- Reopened ticket (CRM-17) → can escalate again only to the **next** level (levels never repeat).
- Notifier throws → logged, rows already saved.
- No supervisor users → notification with role only (nobody reads it until one exists).

## Test Plan

Unit (`TicketEscalationTests`, `SlaMonitorJobTests`), integration (`SlaEscalationTests`), client (`TicketSlaTimers`).

## Verification Steps

`cd server && dotnet build && dotnet test`; `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: warning at 80 % to the assignee (or supervisors when unassigned).
- [ ] AC 2: breach → level + 1, history event, supervisor notification.
- [ ] AC 3: no level twice.
- [ ] AC 4: resolved → no escalation.
- [ ] build / tests / lint green.

## How later stories build on this (write nothing here; for later planners)

- **Notifications UI (Phase 2):** read `Notifications` for the user (`RecipientUserId`) and their roles (`RecipientRole`); replace `LoggingSlaNotifier` with SignalR / e-mail.
- **CRM-18:** show `TicketSlaEvents` (`ResponseWarning`, `ResponseBreached`, `ResolutionBreached`, `Escalated` level n) in the ticket history.
- **CRM-16:** assigning sets `AssigneeId`, which the next warning targets.
