# Story 23 — Email: send replies (Story: CRM-23)

## Prerequisites

- Stories 01–11 on `main` (foundation, security-admin, customer-management). Binding: CRM-9 section 6 ([../03-customer-management/09-story-customer-contacts-CRM-9.md](../03-customer-management/09-story-customer-contacts-CRM-9.md) ~line 2368: `Customer.Email` is the primary email, lower case), CRM-10 section 5 ([../03-customer-management/10-story-customer-timeline-CRM-10.md](../03-customer-management/10-story-customer-timeline-CRM-10.md): `IInteractionRecorder.Record(...)` before `SaveChangesAsync`, `InteractionType.Message` + `messageSent`), CRM-7 permission `channels.manage` (Admin, SuperAdmin) for channel settings ([../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md) line 53).
- **Phase 2 only:** CRM-13 (`Crm.Domain.Tickets.Ticket`, `TicketChannel.Email`, `Ticket.FormatNumber`) and CRM-15 (ticket replies / messages, `FirstResponseAt`) merged to `main`.
- Branch **`feature/group-e-channels`** (CRM-23..26 together). New NuGet: **MailKit 4.18.1** (brings MimeKit 4.18.1) in `Crm.Infrastructure`. One migration: **`AddOutboundMessages`**.

---

## Story Goal

An agent's reply on an email ticket reaches the customer's inbox; a failed send is visible and retried.

1. **[P1]** `IChannelProvider` (Application) is the only way anything is sent (AC 4). `SmtpEmailProvider` (Infrastructure, MailKit) implements it for `ChannelKind.Email`.
2. **[P1]** Every send is logged as an **`OutboundMessage`** (`Pending` → `Sent`, or `Failed` with the error, attempt count and `NextAttemptAt`). `IChannelSender.RetryDueAsync` re-sends due failed messages with back-off 1 / 5 / 15 / 60 minutes, at most **5 attempts**; after that the message stays `Failed` with `NextAttemptAt = null` (AC 3).
3. **[P1]** `TicketNumberTag.AppendTo(subject, number)` puts `[TKT-000001]` into the subject (once — an existing tag is not repeated) (AC 2).
4. **[P1]** `GET /api/channels/status` (`channels.manage`) → `{ email: { configured }, whatsApp: { configured } }`; missing SMTP settings never crash startup — sends then fail with "Email is not configured.".
5. **[P2]** An agent reply on a ticket whose `Channel` is `Email` is sent through `IChannelSender` to the customer's primary email (`Customer.Email`), subject `Re: <ticket subject> [TKT-000001]`, `SourceId` = reply message id; the reply shows its delivery status (AC 1).

**Not in scope:** HTML templates, outgoing attachments, bounces, per-agent mailboxes, SLA.

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules; Architecture decisions ("Channels", "Secrets").
2. `.squad/plans/06-channels/00-overview.md` — shared contracts of the feature.
3. `server/src/Crm.Infrastructure/DependencyInjection.cs` (registrations; lazy configuration reads like `FileStorage:RootPath`), `server/src/Crm.Application/DependencyInjection.cs`.
4. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` (`DbSet` pattern, UTC converter), `Persistence/Configurations/CustomerInteractionConfiguration.cs` (configuration style).
5. `server/src/Crm.Application/Customers/CustomerText.cs` + `Common/Localization/LocalizedText.cs` (text classes; `LocalizedTextCatalogTests` picks up `ChannelText` automatically).
6. `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs` (minimal-API group style), `server/src/Crm.Api/Program.cs` (map + service registration order).
7. Tests: `server/tests/Crm.UnitTests/Customers/TimelineTestDoubles.cs` (`ManualClock`, fakes style); `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` (config injection, `Time`); `Auth/PermissionPolicyTests.cs` (`AdminSettingsEndpoint_AsAgent_Returns403` pattern).

---

## Backend Tasks

All commands from `server/`.

### 1 — Unit tests first (Red) [P1]

`server/tests/Crm.UnitTests/Channels/`:
- `TicketNumberTagTests` — `Format_PadsToSixDigits` (`[TKT-000001]`), `AppendTo_AddsTheTag`, `AppendTo_DoesNotRepeatAnExistingTag`, `TryFind_ReadsTheNumber` (`"Re: help [TKT-000123]"` → 123, case-insensitive), `TryFind_WithoutTag_ReturnsFalse`.
- `OutboundMessageTests` — `Create_StartsPending`, `MarkSent_SetsProviderIdAndSent`, `MarkFailed_SchedulesTheNextAttempt_WithBackOff` (1, 5, 15, 60 min), `MarkFailed_AfterTheLastAttempt_StopsRetrying`, `Create_WithNonUtcTime_Throws`.
- `ChannelSenderTests` (fakes: `FakeChannelProvider`, `FakeOutboundMessageRepository`) — `Send_UsesTheProviderOfTheChannel_AndMarksSent` (AC 4), `Send_WhenTheProviderFails_MarksFailed_AndSchedulesARetry` (AC 3), `Send_WhenTheProviderThrows_MarksFailed`, `Send_WhenNotConfigured_MarksFailed_WithNotConfigured`, `RetryDue_ResendsDueFailedMessages` (AC 3), `RetryDue_SkipsMessagesNotDueYet`.

### 2 — Domain + Application (Green) [P1]

- **Create** `server/src/Crm.Domain/Channels/ChannelKind.cs`, `DeliveryStatus.cs`, `TicketNumberTag.cs`, `OutboundMessage.cs`:

```csharp
public sealed class OutboundMessage
{
    public const int MaxAttempts = 5;
    public Guid Id { get; private set; }
    public ChannelKind Channel { get; private set; }
    public string Recipient { get; private set; }      // email address / E.164 number
    public string? Subject { get; private set; }       // email only, max 300
    public string Body { get; private set; }           // max 10 000
    public string? TemplateName { get; private set; }  // WhatsApp template (CRM-25)
    public Guid? SourceId { get; private set; }        // ticket message id (Phase 2)
    public DeliveryStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }     // max 1000
    public string? ProviderMessageId { get; private set; } // Message-Id / WhatsApp wamid, max 200
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public static OutboundMessage Create(ChannelKind channel, string recipient, string? subject, string body, string? templateName, Guid? sourceId, DateTime utcNow);
    public void MarkSent(string? providerMessageId, DateTime utcNow);
    public void MarkFailed(string error, DateTime utcNow);   // Attempts++, NextAttemptAt by back-off or null after MaxAttempts
    public void ApplyDeliveryStatus(DeliveryStatus status, string? error, DateTime utcNow); // CRM-25
}
```

- **Create** `server/src/Crm.Application/Channels/`: `IChannelProvider` (`ChannelKind Channel`, `bool IsConfigured`, `Task<ChannelSendResult> SendAsync(OutboundChannelMessage, CancellationToken)`), `ChannelContracts.cs` (`OutboundChannelMessage(Guid Id, string Recipient, string? Subject, string Body, string? TemplateName)`, `ChannelSendResult(bool Succeeded, string? ProviderMessageId, string? Error)` + `Ok` / `Fail`, `ChannelReply(ChannelKind Channel, string Recipient, string? Subject, string Body, string? TemplateName, Guid? SourceId)`, `OutboundMessageResponse`, `ChannelStatusResponse`), `IOutboundMessageRepository` (`Add`, `FindByProviderMessageIdAsync`, `ListDueForRetryAsync(DateTime utcNow, int max)`, `SaveChangesAsync`), `IChannelSender` + `ChannelSender` (`SendAsync(ChannelReply)`, `RetryDueAsync`, `GetStatus()`), `ChannelText` (`EmailNotConfigured`, `WhatsAppNotConfigured`, `ProviderMissing`, …), `EmailChannelOptions` (`Smtp { Host, Port = 587, Security = "StartTls", UserName, Password }`, `FromAddress`, `FromName`, `Imap { … }` for CRM-24; `IsSmtpConfigured`).
- Provider exceptions are caught by `ChannelSender` (logged as `LastError`, never thrown to the caller); the sender always saves the `OutboundMessage`.

### 3 — Infrastructure + Api (Green) [P1]

- `Crm.Infrastructure.csproj`: `<PackageReference Include="MailKit" Version="4.18.1" />`.
- **Create** `server/src/Crm.Infrastructure/Channels/Email/ISmtpTransport.cs` + `MailKitSmtpTransport.cs` (connect / authenticate when a user name is set / send / disconnect; not unit-tested — no network in tests), `EmailMessageBuilder.cs` (`MimeMessage` with From = `FromName <FromAddress>`, To, Subject, plain-text body, `Message-Id` `<{id:N}@{from domain}>`), `SmtpEmailProvider.cs` (`IChannelProvider`; not configured → `Fail(ChannelText.EmailNotConfigured)`; transport exception → `Fail(exception.Message)`; success → `Ok(messageId)`).
- **Create** `Persistence/Configurations/OutboundMessageConfiguration.cs` (table `OutboundMessages`, enums as strings max 16, lengths above, index `(Status, NextAttemptAt)`, index `ProviderMessageId`), `DbSet<OutboundMessage> OutboundMessages`, `Crm.Infrastructure/Channels/OutboundMessageRepository.cs`.
- **DI** (`AddInfrastructure`): `EmailChannelOptions` singleton read lazily from `Channels:Email`; `ISmtpTransport` → `MailKitSmtpTransport`; `IChannelProvider` → `SmtpEmailProvider` (scoped); repository. Application: `IChannelSender` → `ChannelSender`.
- **Create** `server/src/Crm.Api/Endpoints/ChannelsEndpoints.cs`: `GET /api/channels/status` `.RequireAuthorization(Permissions.ChannelsManage)`.
- **Create** `server/src/Crm.Api/Channels/ChannelWorker.cs` (`BackgroundService`, every `Channels:WorkerIntervalSeconds` (default 60): new scope → `IChannelSender.RetryDueAsync`; exceptions logged). Registered in `Program.cs` **unless** environment is `Testing`.
- **Migration:** `dotnet ef migrations add AddOutboundMessages --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations`.

### 4 — Infrastructure / integration tests [P1]

`server/tests/Crm.Api.IntegrationTests/Channels/` (the project references Infrastructure through Api):
- `SmtpEmailProviderTests` (fake `ISmtpTransport`) — `Send_BuildsTheMessage_WithSubjectTagAndRecipient` (AC 1, AC 2 on the built `MimeMessage`), `Send_WhenTheTransportThrows_ReturnsFailure` (AC 3), `Send_WhenNotConfigured_ReturnsNotConfigured`.
- `ChannelStatusTests` — status as SuperAdmin 200 (`email.configured` false in tests), Agent 403, no token 401.
- `ChannelRetryTests` — through the real `IChannelSender` + a fake provider replacing the SMTP one: failed send persisted as `Failed`; after `factory.Time.Advance(1 min)` `RetryDueAsync` marks it `Sent` (AC 3).

### 5 — Ticket wiring [P2]

- After `git merge main` (with `CRM-15:`): in the reply use case of CRM-15, when `ticket.Channel == TicketChannel.Email` and the reply is public: `IChannelSender.SendAsync(new ChannelReply(ChannelKind.Email, customer.Email, TicketNumberTag.AppendTo("Re: " + ticket.Subject, ticket.Number), body, null, messageId))`; no primary email → reply saved, delivery `Failed` with `ChannelText.CustomerHasNoEmail`. Record `InteractionType.Message` + `messageSent` via `IInteractionRecorder`.
- Show the delivery status of a reply (`OutboundMessages` by `SourceId`) in the reply response / UI (CRM-15 message list): label "Sent" / "Failed" (en + ar).
- Tests: integration `EmailTicketReply_SendsThroughTheEmailProvider_WithTheTicketNumber` (fake provider captures the message, AC 1/2/4), `EmailTicketReply_WhenSmtpFails_IsFailedAndRetried` (AC 3).

---

## Frontend Tasks

- **[P1]** No frontend changes.
- **[P2]** Delivery status badge on replies of email tickets (in the CRM-15 reply list); strings in `en.json` / `ar.json`.

---

## Edge Cases & Failure Modes

- **SMTP not configured** (no `Channels:Email:Smtp:Host` / `FromAddress`) → startup fine; `IsConfigured` false; sends logged `Failed` with "Email is not configured." and retried like other failures (they succeed once configured).
- **SMTP down / auth error / timeout** → MailKit exception caught in `SmtpEmailProvider` → `Failed`, back-off retry (`OutboundMessage.MarkFailed`).
- **Retry after 5 attempts** → stays `Failed`, `NextAttemptAt` null, never picked by `ListDueForRetryAsync`.
- **Subject already contains the tag** (customer replied to our email) → `AppendTo` does not add a second one.
- **Concurrent worker runs** → one worker per process; the job takes at most 20 due messages per run. Multi-instance hosting is out of scope (documented).
- **Secrets** → `Channels:Email:Smtp:Password` only in user-secrets / env; `appsettings.json` keeps no SMTP values.

---

## Test Plan

1. Unit: `TicketNumberTagTests`, `OutboundMessageTests`, `ChannelSenderTests`; `LocalizedTextCatalogTests` covers `ChannelText`.
2. Integration: `SmtpEmailProviderTests`, `ChannelStatusTests`, `ChannelRetryTests`; `PermissionPolicyTests` picks up `/api/channels/status`.
3. [P2] `EmailTicketReply_*` integration tests.

**Deviations (Phase 1 as built):**
- Deviation: channel registrations live in `Crm.Infrastructure/Channels/ChannelsServiceCollectionExtensions.cs` (`AddChannels()`, called once from `AddInfrastructure`) to keep the shared DI file small for the parallel groups.
- Deviation: `ChannelWorker` is always registered; it returns immediately in the `Testing` environment (checked through `IHostEnvironment` at run time instead of in `Program.cs`).
- Deviation: extra unit tests `Send_ForAChannelWithoutProvider_MarksFailed`, `GetStatus_ReportsWhichChannelsAreConfigured`, `IsDueForRetry_OnlyWhenFailedAndTheTimeHasCome`, `MarkFailed_CutsLongErrors`, `Create_WithoutRecipient_Throws`; `ChannelRetryTests` is one test (`FailedSend_IsStoredAsFailed_AndRetriedWhenDue`) using `factory.WithWebHostBuilder` to replace the providers with a scripted fake. `OutboundMessageResponse` does not expose `ProviderMessageId`. `ChannelValues` (API names `email` / `whatsapp`, `pending` … `failed`) added.
- Results after Phase 1 of CRM-23: `dotnet test` 332 unit + 220 integration; `npm test` 771; build and lint green.

---

## Migration / Rollback

- `AddOutboundMessages`: table `OutboundMessages` only. Rollback: `dotnet ef database update <previous migration>` drops it (send log lost).

---

## Verification Steps

1. **Backend builds:** `server/`: `dotnet build` — 0 warnings, 0 errors.
2. **Backend tests:** `server/`: `dotnet test` — all green.
3. **Frontend:** `client/`: `npm test`, `npm run build`, `npm run lint`.
4. **Regression:** `git grep -n "Password\|AccessToken" -- server/src/Crm.Api/appsettings*.json` → no channel secrets.

---

## Done Criteria

- [x] [P2] Reply on an email ticket sends to the primary email via SMTP — AC 1.
- [x] [P1] Subject tag `[TKT-000001]` (`TicketNumberTagTests`, `SmtpEmailProviderTests`); [P2] used by the reply — AC 2.
- [x] [P1] SMTP failure → `Failed` + retry (`OutboundMessageTests`, `ChannelSenderTests`, `ChannelRetryTests`) — AC 3.
- [x] [P1] Sending only through `IChannelProvider` (`ChannelSenderTests`) — AC 4.
- [x] `dotnet build` / `dotnet test` / `npm test` / `npm run build` / `npm run lint` green.

## Phase 2 as built

- `ITicketReplyDispatcher` gained `ValidateAsync(ticket, templateName)` (runs before the reply is saved) and `DispatchAsync(message, templateName)`; `TicketMessageService.AddAsync` calls both; `AddTicketMessageRequest` has an optional `TemplateName`.
- `ChannelTicketReplyDispatcher` (Application/Tickets, registered before the no-op): Email ticket → primary email contact (else `Customer.Email`), subject `Re: <subject> [TKT-000012]`, `SourceId` = ticket message id; no address → 400 on field `channel` (`ChannelText.CustomerHasNoEmail`). Manual / Portal tickets deliver nothing.
- Delivery status: new `IChannelDeliveryObserver` (called by `ChannelSender` after send, retry and webhook status); `TicketDeliveryObserver` maps the outbound message to `TicketMessage.MarkSent(providerId)` / `MarkFailed()` inside the same unit of work, so a retried message turns Sent on the thread.
- Deviation: no separate timeline entry in the dispatcher; CRM-15 already records `messageSent`.
- Tests: `ChannelTicketReplyDispatcherTests` (unit), `ChannelTicketWiringTests.EmailTicketReply_*` (integration).
