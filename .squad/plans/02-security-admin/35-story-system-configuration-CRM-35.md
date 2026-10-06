# Story 35 — System configuration (Story: CRM-35)

## Prerequisites

- Story 34 (audit log: `IAuditLogger`, `AuditActions`) and the channels feature (`EmailChannelOptions`, `WhatsAppChannelOptions`), tickets (`TicketService`, `ChannelTicketService`, `TicketNumbering`), SLA (`SlaPolicy`, `Ticket.ApplySla`).
- Branch `feature/phase2-group-r`. Reference only: CRM 01 `specs/48-system-configuration/`.
- New package: none (ASP.NET Data Protection ships with the shared framework). Migration: `AddSystemSettings` (table `SystemSettings`, column `Tickets.Prefix` default `TKT-`). New permission: `settings.manage` (SuperAdmin only).

## Story Goal

SuperAdmin edits, from a **Settings** page, the business hours (days, start, end, on/off), the time zone, the ticket number prefix and the email (SMTP/IMAP) and WhatsApp credentials. No code change.

1. **AC 1** — With business hours **on**, SLA due times of **new** tickets (and recalculated ones on priority change) count only minutes inside the configured days/hours in the configured time zone: new Domain type `BusinessCalendar.AddBusinessMinutes(startUtc, minutes)`; `SlaPolicy.ResponseDueAt/ResolutionDueAt(startUtc, calendar)`; `Ticket.ApplySla(policy, calendar)`. Off (default) = today's 24/7 behaviour. Existing tickets never move. The ticket prefix applies to new tickets (stored per ticket in `Tickets.Prefix`, `DisplayNumber = Prefix + 6 digits`).
2. **AC 2** — Secrets (SMTP password, IMAP password, WhatsApp access token, app secret, verify token) are stored **encrypted** (`ISecretProtector` → Data Protection, purpose `Crm.SystemSettings`); `GET /api/settings` returns only `hasXxx` booleans; the audit log of the change never contains them.
3. **AC 3** — Invalid values → 400 with field errors: time zone unknown, no day selected while enabled, start >= end or not `HH:mm`, prefix not 1–10 letters/digits (optional trailing `-`), ports outside 1–65535, security not one of None/Auto/StartTls/SslOnConnect, bad email address.
4. **AC 4** — `GET`/`PUT /api/settings` need `settings.manage`: SuperAdmin 200; Admin/Supervisor/Agent 403; anonymous 401.
5. Each change writes an audit entry `settings.updated` (old/new non-secret values + which secrets were set/cleared).

**Decisions**

- Typed `GET/PUT /api/settings` (not key/value) so validation is per field. Secret semantics on PUT: `null` = keep, `""` = clear, text = replace.
- Storage: table `SystemSettings(Key PK, Value, IsSecret, UpdatedAt)`; `SettingKeys` is the one place that lists keys and which are secret. `ISystemSettingsProvider` (read side, no cache — a change is visible to the next ticket) returns `RuntimeSettings(BusinessCalendar? , TicketPrefix)`.
- Channel credentials: stored values **override** the `Channels:*` configuration; configuration stays the fallback. The singleton option objects are updated in place (`ChannelOptionsApplier`) after each save and once at startup, so the six consumers need no change. Limit: other API instances apply a change after restart.
- Data Protection key ring: `DataProtection:KeysPath` (optional) persists keys to a folder; default is the framework default. Losing the keys makes stored secrets unreadable (they read as "not set", never crash).
- The `[TKT-n]` subject tag in emails keeps the fixed `TKT` token (matching is by number); `Ticket.TryParseNumber` accepts any configured prefix in search.

## Tasks (tests first)

**T1 — Tests (Red)**: unit `BusinessCalendarTests` (inside hours, after hours → next window, spans days, skips weekend, time zone offset, DST-free zone, 1 year); `SlaPolicy`/`Ticket.ApplySla` with calendar; `TicketTests` prefix + `TryParseNumber`; `SystemSettingsServiceTests` (defaults, validation table, secret keep/clear/replace, stored value is not plaintext, response has no secret, audit event has no secret, options applier); `TicketServiceTests`/`ChannelTicketServiceTests` use settings (prefix, business-hours due time). Integration `SystemSettingsTests` (GET defaults, PUT round trip, 400 fields, 403/401, DB row is not plaintext, new ticket after PUT gets prefix and business-hours due time, existing ticket unchanged); `RolePermissionsTests` for `settings.manage`. Client: `api/settings.test.ts`, `SettingsPage.test.tsx` (load, save, secret field empty + "set" hint, 400 shown under field, hidden without permission), nav tests.

**T2 — Domain/Application**: `BusinessCalendar`; `SystemSetting`; `Ticket.Prefix`; `Crm.Application/Settings/`: contracts, `SettingKeys`, `ISystemSettingsService`/`SystemSettingsService`, `ISystemSettingsProvider`, `ISettingsRepository`, `ISecretProtector`, `ChannelOptionsApplier`, validator, `SettingsText`; `TicketService`/`ChannelTicketService`/`TicketNumbering` take the prefix + calendar from the provider.

**T3 — Infrastructure/Api**: `SystemSettingConfiguration`, `SettingsRepository`, `DataProtectionSecretProtector`, DI + `AddDataProtection`, migration `AddSystemSettings`, `Crm.Api/Endpoints/SettingsEndpoints.cs`, apply stored channel settings after `CrmDbInitializer`.

**T4 — Client**: `api/settings.ts`, `features/settings/*` (form sections: business hours, general, email, WhatsApp), `pages/settings/SettingsPage.tsx`, route + nav item (`settings.manage`), `permissions.ts`, i18n en/ar.

**T5 — Verify** (`dotnet build/test`, `npm test/build/lint`).

## Edge cases

- Ticket created outside hours: the clock starts at the next window start. DST gaps: a result inside a skipped hour moves one hour on.
- Disabling business hours keeps the stored days/hours for later.
- Unreadable secret (keys lost) is treated as unset.

## Out of scope

Holidays, per-department hours, key rotation, connection tests, live propagation across instances.

## Deviations (as built)

- Deviation: new NuGet package `Microsoft.AspNetCore.DataProtection` 10.0.11 in `Crm.Infrastructure` (the class library has no ASP.NET shared-framework reference, so the abstractions are not implicit). The plan said "no new package".
- Deviation: `Ticket.FormatNumber` got an overload `(number, prefix)` instead of an optional parameter (existing tests pass the method group to `Select`).
- Request shape: secrets travel in a separate `secrets` object and `GET` returns `secretsSet` booleans (instead of `hasXxx` flags next to each field).
- Security modes are sent lower-case by the client (`none|auto|starttls|sslonconnect`); the server reads them case-insensitively (existing `MailKitSecurity.Parse`).
- Audit: `settings.updated` added to `AuditActions` and to the audit page labels.
- Test host: `Asia/Riyadh` resolves on Windows and Linux (ICU / tzdata); an unresolvable stored zone makes business hours behave as off (24/7).

## Merge note (main with CRM-27..33)

- Overlap with CRM-27 `AppSettings` (key/value, non-null value <= 1000 chars, no secret flag, used for the auto-assign toggle): **kept separate** — `SystemSettings` needs nullable values, encryption and a per-key secret flag; the two tables hold different kinds of settings. `SettingsEndpoints` now maps both groups (`/api/settings/assignment` for `tickets.assign`, `/api/settings` for `settings.manage`).
- Migrations: this branch's own `AddAuditLog` + `AddSystemSettings` were re-generated on top of main's snapshot as one migration `AddAuditLogAndSystemSettings` (the model diff is generated in one step); no migration of main was touched.
- `TicketService` / `ChannelTicketService` take `ISystemSettingsProvider` before main's optional `IAutoAssignmentService`.
