# Story 27 — SLA: automatic ticket assignment (Story: CRM-27)

## Prerequisites

- Stories 12–18 (tickets, assignment CRM-16, history CRM-18) and 23–26 (channel tickets) are on `main`: `Ticket.AssignTo`, `ITicketHistoryRecorder`, `TicketService.CreateAsync`, `ChannelTicketService.AddInboundAsync`, `TicketNumbering.SaveNewAsync` (callback runs before the save, so history joins the same unit of work).
- Permissions: reuses `tickets.assign` (Supervisor, Admin, SuperAdmin). No new permission.
- One migration: **`AddAutoAssignment`** (`AppSettings` table, `AspNetUsers.IsOnDuty`).

---

## Story Goal

New tickets are given to the available agent with the fewest open tickets.

1. A global setting **auto-assign** (default OFF) stored in `AppSettings`. ON: every new ticket (manual `POST /api/tickets` and channel tickets) is assigned to the candidate with the fewest open tickets (AC 1). OFF: unassigned (AC 3).
2. Candidates = users with role `Agent`, `IsActive` and `IsOnDuty` (new flag, default true). Inactive or off-duty agents are skipped (AC 2). Ties: oldest name order, deterministic (`FullName`, then `Id`). No candidate: the ticket stays unassigned, no error.
3. "Open ticket" = status other than Resolved / Closed.
4. History: a `TicketHistoryField.Assignee` entry (old none, new agent name) recorded with **no actor** (system) through `ITicketHistoryRecorder` (AC 4).
5. A failure of the auto-assignment is swallowed (ticket creation must not fail) and the ticket stays unassigned.
6. API for supervisors: `GET /api/settings/assignment` (`{autoAssignEnabled, agents:[{id, fullName, onDuty, openTickets}]}`), `PUT /api/settings/assignment` `{autoAssignEnabled}`, `PUT /api/settings/assignment/agents/{userId}` `{onDuty}`; all `tickets.assign`. UI: settings page "Assignment" (sidebar, `tickets.assign`).

**Not in scope:** rules, teams, round-robin, reassigning existing tickets. Notifying the assignee is CRM-28.

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/05-sla-automation/CRM-27/intake.md`.
2. `server/src/Crm.Application/Tickets/TicketService.cs` (`CreateAsync`), `ChannelTicketService.cs`, `TicketNumbering.cs`, `TicketHistory.cs` (`ITicketHistoryRecorder`).
3. `server/src/Crm.Infrastructure/Tickets/TicketRepository.cs` (`FindAssigneeAsync` shows the active-user-with-role query), `Identity/ApplicationUser.cs`.
4. `server/tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs`, `TicketServiceTests.cs`; `Crm.Api.IntegrationTests/Tickets/TicketCreationTests.cs`.
5. Client: `client/src/pages/sla/SlaPoliciesPage.tsx`, `api/sla-policies.ts`, `app/navigation.ts`, `app/AppRoutes.tsx`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Tickets/AutoAssignmentRulesTests.cs`: `Pick_ReturnsFewestOpenTickets` (AC 1), `Pick_TieBreaksByNameThenId`, `Pick_NoCandidates_ReturnsNull`.
- `Crm.UnitTests/Tickets/AutoAssignmentServiceTests.cs` (fake repository): `On_AssignsLeastLoadedAgent_AndRecordsSystemHistory` (AC 1, 4), `Off_LeavesTheTicketUnassigned` (AC 3), `NoCandidates_LeavesUnassigned`, `RepositoryFailure_IsSwallowed`. Candidate filtering (inactive / off-duty) is done by the repository query and covered in the integration test.
- `Crm.UnitTests/Tickets/TicketServiceTests.cs`: create with auto-assign calls the service (assigned ticket in the response).
- `Crm.Api.IntegrationTests/Tickets/AutoAssignmentTests.cs`: ON + two agents (one with an open ticket, one inactive, one off-duty) → new ticket goes to the free active on-duty agent, history has an `assignee` entry (AC 1, 2, 4); OFF → `assigneeId` null (AC 3); channel-created ticket is assigned too; settings endpoints: 403 for Agent, 200 for Supervisor.

### 2 — Domain / Application

- `Crm.Domain/Settings/AppSetting.cs` (`Key`, `Value`), `Crm.Application/Tickets/AutoAssignmentRules.cs` (`AssignmentCandidate(Guid Id, string Name, int OpenTickets)`, `Pick`).
- `Crm.Application/Tickets/IAutoAssignmentService.cs` + `AutoAssignmentService`: `Task<Guid?> TryAssignAsync(Ticket ticket, DateTime utcNow, CancellationToken)` — reads the setting, picks, `ticket.AssignTo`, records history (`system: true`). `IAssignmentRepository` (`IsAutoAssignEnabledAsync`, `SetAutoAssignEnabledAsync`, `ListCandidatesAsync`, `ListAgentsAsync`, `SetOnDutyAsync`, `SaveChangesAsync`). `IAssignmentSettingsService` (Get / Update / SetOnDuty; `NotFoundException` for an unknown agent).
- `ITicketHistoryRecorder.Record(..., bool system = false)`: system entries have no actor; update `TicketHistoryRecorder` and `FakeTicketHistoryRecorder`.
- `TicketService.CreateAsync` and `ChannelTicketService` (new ticket branch) call `TryAssignAsync` before `TicketNumbering.SaveNewAsync`.

### 3 — Infrastructure / Api

- `ApplicationUser.IsOnDuty` (default true), `AppSettingConfiguration` (table `AppSettings`, key PK max 100), `AssignmentRepository`, DI, `SettingsEndpoints.cs` (`MapSettingsEndpoints`, group `/api/settings/assignment`), `Program.cs`.
- Migration **`AddAutoAssignment`**.

---

## Frontend Tasks

- Tests first: `AssignmentSettingsPage.test.tsx` (toggle sends PUT; on-duty checkbox sends PUT; agents listed with counts).
- `api/assignment-settings.ts`, `features/assignment/*`, `pages/assignment/AssignmentSettingsPage.tsx`, route `/assignment` (`tickets.assign`), sidebar item `assignment`, i18n en + ar.

---

## Edge Cases & Failure Modes

- Two tickets created at once may pick the same agent (count read before save); acceptable, evened out by later tickets.
- Setting row missing = OFF. An unknown agent id in `PUT .../agents/{id}` = 404.
- Auto-assign errors are swallowed in `AutoAssignmentService` (ticket creation continues unassigned).
- Auto-assignment records history with null actor; the history list shows it as a system change.

## Test Plan

Unit (rules, service, ticket service), integration (`AutoAssignmentTests`, migration check `has-pending-model-changes`), client (`AssignmentSettingsPage`).

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`.
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.
3. `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api` says "No changes".

## Done Criteria

- [ ] AC 1: ON assigns the least loaded active agent.
- [ ] AC 2: inactive / off-duty agents skipped.
- [ ] AC 3: OFF leaves the ticket unassigned.
- [ ] AC 4: history entry recorded.
- [ ] build / tests / lint green.
