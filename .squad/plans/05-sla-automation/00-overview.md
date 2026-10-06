# sla-automation — plan overview

Entry point for the **sla-automation** feature (SLA policy per priority, SLA timers on tickets, breach detection, escalation). Stories execute in order by their `NN` prefix; `NN` continues the global sequence after [ticket-management](../04-ticket-management/00-overview.md) (12–18).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 19 | [19-story-sla-policy-CRM-19.md](19-story-sla-policy-CRM-19.md) | SLA policy configuration (SuperAdmin) | CRM-19 | 01–05, 06, 07, 12 |
| 20 | [20-story-sla-timers-CRM-20.md](20-story-sla-timers-CRM-20.md) | SLA timers on tickets | CRM-20 | 19, 13, 14 |
| 21 | [21-story-sla-breach-detection-CRM-21.md](21-story-sla-breach-detection-CRM-21.md) | SLA breach detection (Hangfire job) | CRM-21 | 20 |
| 22 | [22-story-sla-escalation-CRM-22.md](22-story-sla-escalation-CRM-22.md) | SLA escalation rules | CRM-22 | 21 |
| 27 | [27-story-auto-assignment-CRM-27.md](27-story-auto-assignment-CRM-27.md) | Automatic ticket assignment | CRM-27 | 13, 16, 18, 23 |
| 28 | [28-story-alerts-notifications-CRM-28.md](28-story-alerts-notifications-CRM-28.md) | Alerts & notifications (SignalR + email) | CRM-28 | 22, 27 |

## Dependency notes

- **Every story here builds on the foundation** ([../01-foundation/00-overview.md](../01-foundation/00-overview.md)) and **security-admin** ([../02-security-admin/00-overview.md](../02-security-admin/00-overview.md)): ProblemDetails + Application exceptions, `CrmDbContext` / migrations / `TimeProvider` / `CrmApiFactory` (with `FakeTimeProvider` in `Time`), the permission catalogue (`sla.manage` = SuperAdmin only, `tickets.view`), `RequireAuthorization(Permissions.X)`, `PermissionPolicyTests`, client `<Can>` / `RequirePermission`.
- **Ticket-management** ([../04-ticket-management/00-overview.md](../04-ticket-management/00-overview.md)): `TicketPriority` (High / Mid / Low, API names via `TicketValues`, CRM-12) is the key of the SLA policy. CRM-20..22 need the `Ticket` entity (CRM-13 create, CRM-14 list) — built in parallel on `feature/group-b-tickets` and merged into this branch before CRM-20.
- **Story 19 (CRM-19)** adds `Crm.Domain.Sla.SlaPolicy` (one row per priority, minutes as integers, `ResponseDueAt` / `ResolutionDueAt` helpers, seeded defaults High 2 h / 8 h, Mid 4 h / 24 h, Low 8 h / 72 h), `/api/sla-policies` (`sla.manage`), migration `AddSlaPolicies`, the "SLA policy" settings page.
- **Story 20 (CRM-20)** merges CRM-13/14 from `feature/group-b-tickets`, adds `Ticket.ResponseDueAt` / `ResolutionDueAt` (copied from the policy at creation and on `ChangePriority`) and the nullable `FirstResponseAt` / `ResolvedAt` (**set by CRM-15 / CRM-17** through `MarkFirstResponse` / `MarkResolved` / `Reopen`), `PUT /api/tickets/{id}/priority`, migration `AddTicketSlaTimers`, client SLA countdown in the ticket list.
- **Story 21 (CRM-21)** adds Hangfire (SQL Server storage, not started in `Testing`), `SlaMonitorJob` (recurring `sla-monitor`, every minute), `Ticket.ResponseBreached` / `ResolutionBreached`, `TicketSlaEvents` (unique per ticket/type/level), migration `AddSlaBreaches`.
- **Story 22 (CRM-22)** extends the job with the 80 % warning and escalation (`EscalationLevel`, `Escalated` history event, `Notifications` table, `ISlaNotifier` log implementation), migration `AddSlaEscalation`. CRM-18's history recorder should be called when merged.
- **Stories 27–28 (Phase 2)** extend the feature after channels: CRM-27 adds the auto-assign setting (`AppSettings`), `ApplicationUser.IsOnDuty` and `AutoAssignmentService`; CRM-28 turns the `Notification` entity (CRM-22) into per-user in-app notifications (SignalR hub `/hubs/notifications`, e-mail, mark read, unread count) and replaces `LoggingSlaNotifier`.
