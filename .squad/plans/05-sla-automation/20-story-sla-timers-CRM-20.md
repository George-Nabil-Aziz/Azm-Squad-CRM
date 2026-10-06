# Story 20 — SLA timers on tickets (Story: CRM-20)

## Prerequisites

- Story 19 completed: [19-story-sla-policy-CRM-19.md](19-story-sla-policy-CRM-19.md) — `SlaPolicy` (`ResponseDueAt(start)`, `ResolutionDueAt(start)`), `ISlaPolicyRepository.FindAsync(priority)`; its "How later stories build on this" is binding: due times are **copied onto the ticket** at creation, never recomputed from the current policy.
- CRM-13 / CRM-14 merged into `feature/group-d-sla` from `feature/group-b-tickets` (see "Merge" below): [../04-ticket-management/13-story-create-ticket-CRM-13.md](../04-ticket-management/13-story-create-ticket-CRM-13.md) — `Ticket` (`Create(…, utcNow)`, `Priority`, `AssigneeId`, `CreatedAt`), `TicketService.CreateAsync` (number lock → `Add` → `SaveChangesAsync`), `TicketResponse` / `TicketView` / `TicketService.ToResponse`, `TicketRepository.Rows()`, client `api/tickets.ts`, `pages/tickets/TicketsPage.tsx`; and the CRM-14 ticket list.
- `FirstResponseAt` (CRM-15 replies) and `ResolvedAt` (CRM-17 status workflow) do not exist yet → **this story adds them** to `Ticket` as nullable UTC columns with Domain setters; CRM-15 / CRM-17 call them.
- No new packages. One migration: **`AddTicketSlaTimers`**.

## Merge

`git merge feature/group-b-tickets` (first commit containing `CRM-14:` implementation). Conflicts expected in `CrmDbContextModelSnapshot.cs` (and the CRM-19 `AddSlaPolicies` migration ordering, because group B regenerated `AddTicketCategories`): take theirs for the snapshot, delete `*_AddSlaPolicies.*`, re-run `dotnet ef migrations add AddSlaPolicies …` (same name), then continue. Also `.squad/plans/00-index.md`, `en.json` / `ar.json`, `navigation.ts`, `AppRoutes.tsx`, `App.layout.test.tsx` / `App.i18n.test.tsx` label lists — keep both sides.

---

## Story Goal

Each ticket carries its SLA due times and the UI counts down to them.

1. `TicketService.CreateAsync` reads the policy of the ticket priority and stores `ResponseDueAt = CreatedAt + responseMinutes`, `ResolutionDueAt = CreatedAt + resolutionMinutes` on the ticket (AC 1). `TicketResponse` gains `responseDueAt`, `resolutionDueAt`, `firstResponseAt`, `resolvedAt` (UTC, nullable).
2. **Priority change** recalculates both due times from `CreatedAt` with the new priority's **current** policy (AC 2): Domain `Ticket.ChangePriority(priority, SlaPolicy policy, utcNow)`; API `PUT /api/tickets/{id}/priority` `{ priority }` (needs `tickets.manage`) → 200 + ticket. Same priority → no change.
3. The ticket list (and later the details page) shows, for response and resolution: **"2 h 15 min left"**, **"Overdue by 40 min"** or **"Met"** (when `firstResponseAt` / `resolvedAt` is set; "Met late" if after the due time) (AC 3). Computed in the browser from the due time and the current time (`sla-timer.ts`, pure, tested with a fixed `now`), refreshed every 30 s.
4. Changing a policy afterwards leaves existing tickets' due times untouched (AC 4) — only `ChangePriority` and creation read the policy.

**Decisions**

- Due times are **nullable** columns: tickets created before this migration have none (UI shows "—"). Every creation path (manual now, channels CRM-23..26 later) must call `ticket.ApplySla(policy)`; `TicketService` does it in one place (`ApplySlaAsync`).
- Recalculation on priority change starts from **`CreatedAt`** (not "now"): the customer has been waiting since creation; an upgrade to High may make the ticket immediately overdue — intended.
- No policy row for a priority (cannot happen after seeding) → due times stay null, no exception.
- `ChangePriority` is the Domain method CRM-17 reuses; the endpoint lives in `TicketSlaEndpoints` (`/api/tickets/{id}/priority`) so CRM-17 can keep it or move it into its own status/priority endpoints.
- `MarkFirstResponse(utcNow)` / `MarkResolved(utcNow)` / `Reopen()` Domain methods are added for CRM-15 / CRM-17 (first response is kept once set; `MarkResolved` overwrites, `Reopen` clears `ResolvedAt`).

**Not in scope:** breach flags / job (CRM-21), warnings / escalation (CRM-22), pausing SLA while pending, business hours, the ticket details page (CRM-15).

---

## Context — Read These Files First

1. `CLAUDE.md`; `.squad/stories/05-sla-automation/CRM-20/intake.md`.
2. `server/src/Crm.Domain/Tickets/Ticket.cs`, `server/src/Crm.Domain/Sla/SlaPolicy.cs`.
3. `server/src/Crm.Application/Tickets/TicketService.cs`, `TicketContracts.cs`, `ITicketRepository.cs`, `TicketText.cs`; `server/src/Crm.Application/Sla/ISlaPolicyRepository.cs`.
4. `server/src/Crm.Infrastructure/Tickets/TicketRepository.cs`, `Persistence/Configurations/TicketConfiguration.cs`.
5. `server/tests/Crm.UnitTests/Tickets/TicketTestDoubles.cs`, `TicketServiceTests.cs`; `server/tests/Crm.UnitTests/Sla/SlaTestDoubles.cs`; `server/tests/Crm.Api.IntegrationTests/Tickets/TicketCreationTests.cs`, `TicketBodies.cs`.
6. Client: `client/src/api/tickets.ts`, `client/src/pages/tickets/TicketsPage.tsx` (+ CRM-14 list components), `client/src/features/sla/sla-format.ts`.

---

## Backend Tasks

### 1 — Unit tests first (Red)

- `Crm.UnitTests/Sla/TicketSlaTests.cs` — `ApplySla_SetsDueTimesFromCreatedAt` (AC 1), `ApplySla_WithThePolicyOfAnotherPriority_Throws`, `ChangePriority_RecalculatesFromCreatedAt` (AC 2), `ChangePriority_SamePriority_KeepsDueTimes`, `MarkFirstResponse_KeepsTheFirstTime`, `MarkResolved_ThenReopen_ClearsResolvedAt`.
- `Crm.UnitTests/Tickets/TicketServiceTests.cs` — `Create_SetsDueTimesFromThePolicyOfThePriority` (AC 1), `Create_WhenThePolicyChangesLater_KeepsTheDueTimes` (AC 4), `ChangePriority_RecalculatesTheDueTimes` (AC 2), `ChangePriority_InvalidPriority_Throws400`, `ChangePriority_UnknownTicket_Throws404`. (`TicketService` gets `ISlaPolicyRepository`; tests use `FakeSlaPolicyRepository`.)

### 2 — Domain + Application (Green)

- `Ticket`: `ResponseDueAt`, `ResolutionDueAt`, `FirstResponseAt`, `ResolvedAt` (`DateTime?`); `ApplySla(SlaPolicy)`, `ChangePriority(TicketPriority, SlaPolicy?, DateTime utcNow)`, `MarkFirstResponse`, `MarkResolved`, `Reopen`.
- `TicketContracts`: `TicketResponse` + `ResponseDueAt`, `ResolutionDueAt`, `FirstResponseAt`, `ResolvedAt`; `ChangeTicketPriorityRequest(string? Priority)`.
- `ITicketRepository.FindAsync(Guid id)` (tracked).
- `TicketService`: ctor + `ISlaPolicyRepository slaPolicies`; `CreateAsync` applies the policy; `ChangePriorityAsync(id, request)` (invalid / missing priority → 400 `errors.priority`).

### 3 — Integration tests (Red → Green)

- `Crm.Api.IntegrationTests/Sla/TicketSlaTimersTests.cs` — `CreateTicket_High_GetsDueTimesFromThePolicy` (AC 1, clock = `factory.Time`), `ChangingThePriority_RecalculatesTheDueTimes` (AC 2), `ChangingThePolicy_DoesNotMoveExistingDueTimes` (AC 4), `ChangePriority_Invalid_Returns400`, `ChangePriority_AsUserWithoutTicketsManage_Returns403`, policy dictionary for `PUT /api/tickets/{id:guid}/priority` (tickets.manage + tickets.view).

### 4 — Infrastructure + Api

- `TicketConfiguration`: nothing special (nullable `DateTime?` by convention); index on `ResponseDueAt` and `ResolutionDueAt` for the CRM-21 job.
- `TicketRepository.FindAsync`.
- `Crm.Api/Endpoints/TicketSlaEndpoints.cs` — `MapTicketSlaEndpoints()`: group `/api/tickets` `.RequireAuthorization(Permissions.TicketsView)`; `PUT "/{id:guid}/priority"` `.RequireAuthorization(Permissions.TicketsManage)`. `Program.cs` maps it.
- Migration **`AddTicketSlaTimers`** (four nullable columns + two indexes).

---

## Frontend Tasks

### 1 — Tests first (Red)

- `client/src/features/sla/sla-timer.test.ts` — `slaTimer(dueAt, metAt, now)` → `{ state: 'none' }` without due time; `{ state: 'remaining', minutes }`; `{ state: 'overdue', minutes }`; `{ state: 'met' }` / `{ state: 'metLate' }`.
- `client/src/features/sla/TicketSlaTimers.test.tsx` — renders "Response: 1 h left" / "Resolution: Overdue by 30 min" / "Met" with a fixed `now`.
- Ticket list test (CRM-14 page test): the row shows the SLA timers.
- `client/src/api/tickets.test.ts` — `changeTicketPriority('t1', 'high')` → `PUT /api/tickets/t1/priority`.

### 2 — Implementation (Green)

- `api/tickets.ts`: `Ticket` + the four fields; `changeTicketPriority`.
- `features/sla/sla-timer.ts`, `features/sla/TicketSlaTimers.tsx` (uses `formatMinutes`; badge variants `secondary` / `destructive` / `outline` — theme colors only), `features/sla/useNow.ts` (`useSyncExternalStore`-free simple `useState` + `setInterval(30 s)`).
- Ticket list: SLA column with `<TicketSlaTimers ticket={…} />`.
- i18n `sla.timer.*` (en + ar): response, resolution, left, overdue, met, metLate, none.

---

## Edge Cases & Failure Modes

- Ticket created before this migration → due times null → UI "—"; job (CRM-21) skips it.
- Priority upgraded after the response window passed → immediately overdue (intended, from `CreatedAt`).
- Policy changed between creation and priority change → the new priority uses the **current** policy.
- Client clock skew → countdown off by the skew; acceptable (server time decides breaches in CRM-21).
- `FirstResponseAt` set → response timer shows "Met" (or "Met late"), never counts again.

---

## Test Plan

Unit (`TicketSlaTests`, `TicketServiceTests`), integration (`TicketSlaTimersTests`, existing ticket tests), client (`sla-timer`, `TicketSlaTimers`, list page, api). `PermissionPolicyTests` guards new route.

## Verification Steps

`cd server && dotnet build && dotnet test`; `cd client && npm test && npm run build && npm run lint` — all green, 0 warnings.

## Done Criteria

- [ ] AC 1: due times from the priority's policy at creation.
- [ ] AC 2: `PUT /api/tickets/{id}/priority` recalculates.
- [ ] AC 3: list shows remaining / overdue / met for both timers (en + ar).
- [ ] AC 4: policy change leaves existing tickets unchanged.
- [ ] build / tests / lint green.

---

## How later stories build on this (write nothing here; for later planners)

- **CRM-15 (replies):** on the first **public agent reply** call `ticket.MarkFirstResponse(utcNow)` (keeps the first value). This stops the response timer and is what CRM-21 checks for "replied within its response time".
- **CRM-17 (status workflow):** on → Resolved call `ticket.MarkResolved(utcNow)`; on reopen call `ticket.Reopen()`; priority changes go through `Ticket.ChangePriority(priority, policy, utcNow)` (or reuse `PUT /api/tickets/{id}/priority`).
- **CRM-21:** job compares `UtcNow` with `ResponseDueAt` / `ResolutionDueAt` and checks `FirstResponseAt` / `ResolvedAt`; indexes exist.
- **CRM-23..26 (channels):** after `Ticket.Create(…)` call `ticket.ApplySla(policy)` (or go through a shared `TicketService` path).
