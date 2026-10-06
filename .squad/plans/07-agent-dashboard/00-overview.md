# agent-dashboard — plan overview

Entry point for the **agent-dashboard** feature (Phase 2): what an agent needs while working tickets — their assigned tickets by SLA urgency, customer context beside the ticket, tasks and reminders, quick replies, @mentions. Stories execute in order by their `NN` prefix; `NN` continues the global sequence after [sla-automation](../05-sla-automation/00-overview.md) (27–28).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 29 | [29-story-my-assigned-tickets-CRM-29.md](29-story-my-assigned-tickets-CRM-29.md) | My assigned tickets (dashboard) | CRM-29 | 14, 16, 20–22 |
| 30 | [30-story-customer-info-panel-CRM-30.md](30-story-customer-info-panel-CRM-30.md) | Customer info side panel | CRM-30 | 8, 9, 14 |
| 31 | [31-story-tasks-reminders-CRM-31.md](31-story-tasks-reminders-CRM-31.md) | Tasks & reminders | CRM-31 | 28 |
| 32 | [32-story-quick-replies-CRM-32.md](32-story-quick-replies-CRM-32.md) | Quick replies with placeholders | CRM-32 | 15 |
| 33 | [33-story-team-collaboration-CRM-33.md](33-story-team-collaboration-CRM-33.md) | Team collaboration (@mentions) | CRM-33 | 15, 28 |

## Dependency notes

- **Foundation, security-admin, ticket-management** as in the other overviews: ProblemDetails + Application exceptions, `CrmDbContext` / migrations / `TimeProvider` / `CrmApiFactory`, the permission catalogue and `RequireAuthorization(Permissions.X)`, client `<Can>` / i18n / typed API client.
- **Notifications (CRM-28)** are the delivery channel for reminders (31) and mentions (33): both call `INotificationDispatcher.NotifyAsync` with their own `DedupKey` and `NotificationType` (`TaskReminder`, `Mention`).
- **Dashboard (29)** extends the existing `DashboardPage` (welcome + API status) and adds `GET /api/tickets/mine`; the SLA "next due" rule lives in the Domain (`Ticket.NextSlaDueAt`).
- **Migrations:** 31 (`AddTasks`) and 32 (`AddQuickReplies`) add tables; 29, 30, 33 need none.
- New permissions are listed in each story; the client keeps `permissions.ts` equal to the server catalogue.
