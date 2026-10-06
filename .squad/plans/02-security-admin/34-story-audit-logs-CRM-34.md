# Story 34 — Audit logs (Story: CRM-34)

## Prerequisites

- Merged to `main`: CRM-2 (login, `AuthService`), CRM-6 (`UserService`), CRM-7 (permission catalogue `Permissions` / `RolePermissions`, `ICurrentUser`), CRM-8..11 (customers, attachments), CRM-19 (`SlaPolicyService`).
- Branch `feature/phase2-group-r`. Reference only (behaviour, not code): CRM 01 `specs/47-audit-logs/`.
- New package: none. New migration: `AddAuditLog` (table `AuditLog`). New permission: `audit.view`.

## Story Goal

1. Every sensitive action is written to an append-only audit log with **user, action, entity, old/new values, IP, time** (AC 1): `login.succeeded`, `login.failed` (AC 2, also for unknown email / locked / inactive user — the reason goes in `newValues`, never in the HTTP answer), `user.created|updated|deactivated|reactivated` (role changes show in old/new roles), `sla-policy.updated`, `customer.deleted`, `customer-contact.removed`, `customer-attachment.deleted`.
2. `GET /api/audit-logs?userId=&action=&from=&to=&page=&pageSize=` returns a `PagedResult<AuditLogResponse>`, newest first (AC 3). `from > to`, an unknown `action`, or paging out of range → 400 with field errors.
3. Read-only and restricted (AC 4): only `GET` exists (other verbs → 405/404); `RequireAuthorization(Permissions.AuditView)`; SuperAdmin + Admin have it, Supervisor/Agent get 403.
4. Client: sidebar item + page "Audit log" (table, filters user / action / date range, paging), visible with `audit.view`.

**Decisions**

- Explicit logging from the Application/Infrastructure services through `IAuditLogger` (not an EF interceptor): Identity role changes and soft deletes are easier to describe explicitly, and the actor/IP come from `ICurrentUser` / `IClientInfo`.
- The logger saves immediately after the action succeeded (own `SaveChanges`); a failed action is not logged, except failed logins.
- `UserEmail` is stored only when there is no signed-in user (login attempts); otherwise the read query joins `Users` so a renamed user shows the current email.
- IP = `HttpContext.Connection.RemoteIpAddress` (no `X-Forwarded-For` trust; reverse-proxy forwarding is a deployment concern).
- Old/new values are small JSON objects (`System.Text.Json`), never secrets or passwords.

## Tasks (tests first)

**T1 — Tests (Red)**
- Unit `AuditLoggerTests`: stamps time from `TimeProvider`, user from `ICurrentUser`, IP from `IClientInfo`; explicit user wins (login).
- Unit `AuditLogServiceTests` / `ListAuditLogsQueryValidatorTests`: `from > to`, unknown action, page size > 100 → `ValidationException`; filters passed to the repository.
- Unit service tests: `SlaPolicyService.UpdateAsync`, `CustomerService.DeleteAsync/RemoveContactAsync`, `CustomerAttachmentService.DeleteAsync` write one event with old/new values (fake `IAuditLogger`).
- Integration `AuditLogTests`: AC 1 (create user via API → entry with user, action, entity, new values, time; IP field present), AC 2 (wrong password → `login.failed`, email, no password in values), AC 3 (filter by user, action, from/to), AC 4 (Agent/Supervisor 403, 401 anonymous, Admin + SuperAdmin 200, `POST/PUT/DELETE /api/audit-logs` not allowed), role change → `user.updated` with old/new roles, SLA change, customer delete.
- `RolePermissionsTests`: `audit.view` for SuperAdmin + Admin only; client `permissions.test.ts` stays in sync.
- Client: `api/audit-logs.test.ts` (query string), `AuditLogsPage.test.tsx` (rows, filters call the API with params, paging), sidebar item hidden for Agent (`App.permissions.test.tsx` style).

**T2 — Domain/Application**: `Crm.Domain/Audit/AuditLogEntry` + `AuditActions`; `Crm.Application/Audit/`: `IAuditLogger`, `AuditLogger`, `IClientInfo`, `IAuditLogRepository`, `IAuditLogService`/`AuditLogService`, contracts, `ListAuditLogsQueryValidator`, `AuditText`. Add `Permissions.AuditView`.

**T3 — Infrastructure/Api**: `AuditLogConfiguration` (indexes on `OccurredAt`, `(UserId, OccurredAt)`, `(Action, OccurredAt)`), `AuditLogRepository`, DI, migration `AddAuditLog`; `Crm.Api/Endpoints/AuditLogsEndpoints.cs` (`MapAuditLogsEndpoints`), `HttpClientInfo`; instrument `AuthService`, `UserService`, `SlaPolicyService`, `CustomerService`, `CustomerAttachmentService`.

**T4 — Client**: `api/audit-logs.ts`, `features/audit/` (hook, filters, table), `pages/audit/AuditLogsPage.tsx`, route + `navigation.ts` item (`permission: audit.view`), `permissions.ts` (+`auditView`), i18n en/ar.

**T5 — Verify**: `dotnet build` (0 warnings), `dotnet test`, `npm test`, `npm run build`, `npm run lint`.

## Edge cases

- Failed login for an unknown email: `UserId` null, `UserEmail` = attempted email (trimmed, max 256).
- Dates are UTC; `from`/`to` are inclusive instants.
- Audit failure must not leak into the login answer: a logging exception is not swallowed (it is a server error), but the message of a failed login stays the generic one.

## Out of scope

Audit of denied attempts, ticket/customer edits, retention, export.
