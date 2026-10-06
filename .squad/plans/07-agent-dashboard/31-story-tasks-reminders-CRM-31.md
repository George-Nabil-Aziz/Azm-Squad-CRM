# Story 31 — Tasks & reminders (Story: CRM-31)

## Prerequisites

- [../05-sla-automation/28-story-alerts-notifications-CRM-28.md](../05-sla-automation/28-story-alerts-notifications-CRM-28.md): `INotificationDispatcher`, `NotificationType.TaskReminder`, in-app bell. CRM-21 Hangfire wiring (`RecurringJobs`, not started in `Testing`). CRM-13 tickets (optional link).
- New permission **`tasks.manage`** (every role). One migration: **`AddTasks`**.

---

## Story Goal

Agents keep their own follow-ups and are reminded when they are due.

1. `POST /api/tasks` `{title, dueAt, ticketId?, description?}` → **201** with the task (AC 1). Title required (max 200), `dueAt` required (UTC ISO); **a due date in the past → 400** with a `dueAt` field error (AC 2); an unknown `ticketId` → 400 on `ticketId`.
2. `GET /api/tasks?status=open|done` (default `open`): the signed-in user's own tasks, open ones by due time (AC 4 view); `POST /api/tasks/{id}/done` marks it Done (404 for somebody else's task); done tasks leave the open list (AC 4). Users only ever see their own tasks.
3. **Reminder** (AC 3): `TaskReminderJob.RunAsync` (recurring job `task-reminders`, every minute) finds open tasks whose `DueAt <= now` and `ReminderSentAt == null`, sends a `TaskReminder` notification (key `task-reminder:{taskId}`, text = title, the linked ticket opens from the bell) through `INotificationDispatcher` and sets `ReminderSentAt`. A task done before its due time gets no reminder.
4. UI: sidebar item "Tasks" (`tasks.manage`), page with a create form (title, due date and time, description) and the open tasks with a "Mark as done" button; a past date is rejected by the form and by the API.

**Not in scope:** recurring tasks, assigning tasks to others, editing, comments, email reminders.

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/07-agent-dashboard/CRM-31/intake.md`.
2. `server/src/Crm.Infrastructure/Jobs/JobsExtensions.cs` (`RecurringJobs.Register`), `Crm.Application/Sla/SlaMonitorJob.cs` (job pattern), `Crm.Application/Notifications/NotificationContracts.cs` (`NotificationRequest`), `Crm.Application/Auth/Permissions.cs`, `RolePermissions.cs`.
3. `server/src/Crm.Application/Customers/Notes/CustomerNoteService.cs` (service + validator + current user pattern), `Crm.Infrastructure/Persistence/Configurations/CustomerNoteConfiguration.cs`.
4. Tests: `Crm.UnitTests/Auth/RolePermissionsTests.cs`, `Crm.Api.IntegrationTests/Sla/SlaJobScheduleTests.cs`, `Auth/ProtectedEndpointTests.cs` (agent permissions), client `auth/permissions.ts`, `test/fake-api.ts`.
5. Client: `pages/sla/SlaPoliciesPage.tsx` + `features/sla/SlaPolicyFormDialog.tsx` (react-hook-form + zod pattern), `app/navigation.ts`, `app/AppRoutes.tsx`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Tasks/WorkTaskTests.cs`: create sets fields; past due rejected by the domain; `MarkDone` once; `MarkReminderSent` once.
- `Crm.UnitTests/Tasks/TaskServiceTests.cs`: create 201 shape (AC 1), past `dueAt` → `ValidationException` on `dueAt` (AC 2), blank title, unknown ticket, list open only mine (AC 4), done removes it, foreign task → `NotFoundException`.
- `Crm.UnitTests/Tasks/TaskReminderJobTests.cs`: due task → one `TaskReminder` request with title, ticket and key, `ReminderSentAt` set (AC 3); not yet due → nothing; done task → nothing; second run → nothing new.
- `Crm.Api.IntegrationTests/Tasks/TasksApiTests.cs`: 201 / 400 past / 401 / list + done flow / other user's task 404 / job through the DB creates the bell notification for the owner at the due time (fake clock) and only once (AC 3).
- `SlaJobScheduleTests`: both recurring jobs registered; `RolePermissionsTests`, `ProtectedEndpointTests`: `tasks.manage`.

### 2 — Domain / Application

- `Crm.Domain/Tasks/WorkTask.cs` (`OwnerId`, `Title`, `Description`, `DueAt`, `TicketId?`, `CompletedAt?`, `ReminderSentAt?`, `CreatedAt`; `Create`, `MarkDone`, `MarkReminderSent`, `IsDone`).
- `Crm.Application/Tasks/`: `TaskContracts.cs` (`CreateTaskRequest`, `TaskResponse`, `ListTasksQuery`), validator, `ITaskRepository`, `ITaskService` + `TaskService`, `TaskText`, `TaskReminderJob`. `Permissions.TasksManage`.

### 3 — Infrastructure / Api

- `WorkTaskConfiguration` (table `Tasks`, FK `TicketId` Restrict, FK `OwnerId` → users, index `(OwnerId, CompletedAt, DueAt)` and `(ReminderSentAt, DueAt)`), `TaskRepository`, DI, `TasksEndpoints.cs` (`/api/tasks`, `tasks.manage`), `RecurringJobs` + `task-reminders`, migration **`AddTasks`**.

---

## Frontend Tasks

- Tests first: `pages/tasks/TasksPage.test.tsx` (list, create sends ISO date, past date rejected client side and server 400 shown, mark done reloads), `api/tasks.test.ts`.
- `api/tasks.ts`, `features/tasks/*`, `pages/tasks/TasksPage.tsx`, route `/tasks`, sidebar item, `permissions.ts` `tasksManage`, i18n en + ar.

---

## Edge Cases & Failure Modes

- Reminder job runs late (server down): reminders for every missed due task go out on the next run (`DueAt <= now`).
- Notification already stored (job retry): the dispatcher dedupes by key; `ReminderSentAt` is still set.
- Task owner deactivated: the dispatcher skips inactive users; `ReminderSentAt` is set anyway.
- Clock: `dueAt` must be later than `TimeProvider` now; equal is rejected.
- A deleted-ticket link cannot happen (tickets are never deleted).

## Test Plan

Unit (domain, service, job), integration (API + job against the database), client (page, API).

## Migration / Rollback

`AddTasks` creates the `Tasks` table; rollback drops it.

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`; `dotnet ef migrations has-pending-model-changes ...` → "No changes".
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: create → 201.
- [ ] AC 2: past due date → 400.
- [ ] AC 3: reminder notification at the due time, once.
- [ ] AC 4: Done removes the task from the open list.
- [ ] build / tests / lint green.
