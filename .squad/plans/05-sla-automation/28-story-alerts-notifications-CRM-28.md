# Story 28 — SLA: alerts & notifications (Story: CRM-28)

## Prerequisites

- [22-story-sla-escalation-CRM-22.md](22-story-sla-escalation-CRM-22.md): `Notification` entity, `ISlaNotifier`, `SlaMonitorJob`; [27-story-auto-assignment-CRM-27.md](27-story-auto-assignment-CRM-27.md): `IAutoAssignmentService`; CRM-16 `TicketAssignmentService`; CRM-23 `ISmtpTransport` + `EmailChannelOptions` (`Channels:Email`).
- New package: `Microsoft.AspNetCore.SignalR.Client` (tests only), `@microsoft/signalr` (client).
- New permission **`notifications.view`** (all four roles) for the notification endpoints and the hub.
- One migration: **`AddInAppNotifications`**.

---

## Story Goal

1. **Per-user notifications.** `Notification` becomes one row per user (`RecipientUserId`), with `Type` (`Assignment`, `SlaWarning`, `SlaEscalation`; later `TaskReminder`, `Mention`), optional `TicketId`, `Level`, free `Text`, `DedupKey`, `ReadAt`. Role recipients (supervisors) are expanded to the active users of the role when the notification is created (CRM-22 stored role rows; they are replaced).
2. **Assignment** (AC 1): assigning a ticket to someone else (`TicketAssignmentService`, auto-assignment CRM-27) creates an `Assignment` notification for the assignee and pushes it through the SignalR hub `/hubs/notifications` at once. Assigning to yourself notifies nobody.
3. **SLA** (AC 2): `SlaMonitorJob` warning (80 %) and escalation produce an in-app notification **and an email** (SMTP, `Channels:Email`; when SMTP is not configured the email is skipped, never an error). `LoggingSlaNotifier` is replaced by `SlaNotifier` (Application) which uses the dispatcher; the job no longer writes `Notification` rows itself.
4. **Read state** (AC 3): `GET /api/notifications?unreadOnly&page&pageSize`, `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read` (404 for somebody else's), `POST /api/notifications/read-all`. UI: bell in the header with the unread count, a panel listing notifications (link to the ticket, "Mark as read", "Mark all as read"), live updates through the hub.
5. **No duplicates** (AC 4): `DedupKey` identifies the event (`sla-warning:{ticket}`, `sla-escalation:{ticket}:{level}`, `assignment:{ticket}:{assignee}:{ticks}`); unique index `(RecipientUserId, DedupKey)` (filtered on non-null users) + a pre-check; a second `NotifyAsync` with the same key stores, pushes and mails nothing.

**Decisions.** `INotificationDispatcher` (Application) does recipients → rows → save → push (`INotificationPublisher`, SignalR in Api, no-op default) → email (`INotificationEmailSender`, MailKit in Infrastructure); push / email failures are swallowed. Users are matched through `IStaffDirectory` (active users only). SignalR user id = the JWT `sub` claim (custom `IUserIdProvider`); the token comes from `access_token` in the query string for hub requests. Emails are bilingual (English + Arabic) because the recipient language is unknown.

**Not in scope:** push / SMS, preferences, digests, notifications for other types (stories 31, 33 add theirs through the same dispatcher).

---

## Context — Read These Files First

1. `CLAUDE.md`; `server/src/Crm.Domain/Notifications/Notification.cs`, `Crm.Application/Sla/ISlaNotifier.cs`, `SlaMonitorJob.cs`, `Crm.Infrastructure/Sla/LoggingSlaNotifier.cs`, `Persistence/Configurations/NotificationConfiguration.cs`.
2. `Crm.Application/Tickets/TicketAssignmentService.cs`, `AutoAssignment.cs` (CRM-27), `Crm.Infrastructure/Channels/Email/ISmtpTransport.cs`, `Crm.Application/Channels/EmailChannelOptions.cs`.
3. `Crm.Api/Auth/AuthenticationExtensions.cs` (`JwtBearerEvents`), `Program.cs`, `Crm.Application/Auth/Permissions.cs`, `RolePermissions.cs`, client `auth/permissions.ts`.
4. Tests: `Crm.UnitTests/Sla/SlaMonitorJobTests.cs` (job fakes), `Crm.Api.IntegrationTests/Sla/SlaEscalationTests.cs`, `Channels/SmtpEmailProviderTests.cs`, `Auth/PermissionPolicyTests.cs`.
5. Client: `components/layout/AppHeader.tsx`, `api/client.ts`, `auth/session.ts`, `test/setup.ts`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Notifications/NotificationTests.cs`: `ForUser_SetsFields_AndMarkRead_IsIdempotent`, UTC check.
- `Crm.UnitTests/Notifications/NotificationDispatcherTests.cs` (fakes): `Notify_StoresOneRowPerRecipient_PushesAndCounts` (AC 1), `Notify_Role_ExpandsToActiveUsers`, `Notify_SameKeyTwice_NotifiesOnce` (AC 4), `Sla_Types_SendAnEmail_Assignment_DoesNot` (AC 2), `PublisherOrEmailFailure_DoesNotFail`, `NoEmailAddress_SkipsTheEmail`.
- `Crm.UnitTests/Notifications/SlaNotifierTests.cs`: warning / escalation notices map to the right type, key and recipients.
- `Crm.UnitTests/Notifications/NotificationServiceTests.cs`: list, unread count, mark read (404 for foreign), read all.
- `Crm.UnitTests/Tickets/TicketAssignmentServiceTests.cs`: assigning to another user notifies (AC 1); self-assignment and unassign do not.
- `SlaMonitorJobTests` / `SlaEscalationTests`: assert notices (fake notifier) and per-user rows; remove `AddNotification`.
- `Crm.Api.IntegrationTests/Notifications/NotificationsApiTests.cs`: list / unread-count / mark read / read-all / foreign 404 / 401 anonymous (AC 3); assignment through `POST /api/tickets/{id}/assign` creates the row; SLA warning creates an in-app row for the assignee and sends an email through a fake `ISmtpTransport` (AC 2); running the job twice → one row, one email (AC 4).
- `Crm.Api.IntegrationTests/Notifications/NotificationHubTests.cs`: a `HubConnection` (test server handler, long polling, `access_token`) receives `notification` when its user is assigned a ticket (AC 1); unauthenticated connection is refused.
- `RolePermissionsTests` / `PermissionPolicyTests`: `notifications.view` for every role.

### 2 — Domain / Application / Infrastructure / Api

- `Notification`: `ForUser(userId, ticketId?, type, level, dedupKey, text, utcNow)`, `MarkRead(utcNow)`, `Text`, `DedupKey`; remove `ForRole` / `RecipientRole`; `NotificationType` + `Assignment`, `TaskReminder`, `Mention`.
- `Crm.Application/Notifications/`: contracts, `INotificationRepository`, `INotificationService` + `NotificationService`, `INotificationDispatcher` + `NotificationDispatcher`, `IStaffDirectory`, `INotificationPublisher` (+ `NoopNotificationPublisher`), `INotificationEmailSender`, `SlaNotifier`, `NotificationText` (localized email text). `ITicketSlaRepository.AddNotification` removed. `TicketAssignmentService` and `AutoAssignmentService.NotifyAssignedAsync` dispatch `Assignment`.
- Infrastructure: `NotificationRepository`, `StaffDirectory`, `NotificationEmailSender`, configuration (`TicketId` nullable, `Text` 500, `DedupKey` 200, filtered unique index), delete `LoggingSlaNotifier`.
- Api: `NotificationsHub`, `NotificationUserIdProvider`, `SignalRNotificationPublisher`, `NotificationsEndpoints`, `AddSignalR`, `MapHub("/hubs/notifications").RequireAuthorization(Permissions.NotificationsView)`, `OnMessageReceived` reading `access_token` for `/hubs`.
- Migration **`AddInAppNotifications`**.

---

## Frontend Tasks

- Tests first: `features/notifications/NotificationBell.test.tsx` (unread badge, panel lists notifications, mark one / all read, live message increments), `api/notifications.test.ts`.
- `api/notifications.ts`, `notifications/hub.ts` (SignalR connection wrapper, mocked in `test/setup.ts`), `features/notifications/*` (hook, bell, panel), header integration, i18n en + ar (`notifications.*`), `permissions.ts` `notificationsView`.

---

## Edge Cases & Failure Modes

- SMTP not configured / send fails: row and push still happen (`NotificationDispatcher` catches).
- Concurrent job runs: unique index rejects the second save; the dispatcher treats it as "already notified".
- Role with no active users: nothing stored.
- Deactivated user: not a recipient (`IStaffDirectory` lists active users only).
- A user connected twice (two tabs) receives the push on both (`Clients.User`).
- Expired JWT on a hub request: 401, the client reconnects with a fresh token on the next login.
- CRM-22 role rows (`RecipientUserId` null) stay in the table and are ignored.

## Test Plan

Unit (Notification, dispatcher, SlaNotifier, service, assignment, job), integration (API, hub, email), client (bell, api).

## Migration / Rollback

`AddInAppNotifications`: drops `RecipientRole`, adds `Text`, `DedupKey` (default empty), makes `TicketId` nullable, adds the filtered unique index. Rollback = `Down`.

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`; `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api` says "No changes".
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: assignment notifies the assignee in real time (hub test).
- [ ] AC 2: warning and breach: in-app row + email.
- [ ] AC 3: mark read, read all, unread count in API and UI.
- [ ] AC 4: the same event never notifies a user twice.
- [ ] build / tests / lint green.

## Implementation notes (deviations)

> **Deviation:** `INotificationDispatcher` is an optional last constructor parameter of `TicketAssignmentService`; `AutoAssignmentService` gets `NotifyAssignedAsync` (called after the ticket was saved, never for self-assignment). `Notification.RecipientUserId` stays nullable (legacy CRM-22 role rows are ignored). The hub pushes `{ notification, unreadCount }` to the `notification` method. `ISlaNotifier` is registered by the Application layer (`SlaNotifier`); `LoggingSlaNotifier` was deleted. The client mocks `api/notifications-hub` globally in `test/setup.ts`. `GET /api/notifications` uses `pageSize` 20 by default; the panel shows the first page. The test host (`WithWebHostBuilder`) replaces `ISmtpTransport` to assert emails.
