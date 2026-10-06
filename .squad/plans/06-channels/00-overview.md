# channels — plan overview

Entry point for the **channels** feature (email and WhatsApp: sending agent replies, turning incoming messages into tickets). Stories execute in order by their `NN` prefix; `NN` continues the global sequence (12–22 are the ticket-management and SLA stories built in parallel on other branches).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 23 | [23-story-email-send-replies-CRM-23.md](23-story-email-send-replies-CRM-23.md) | Email: send replies | CRM-23 | 01–11; Phase 2: 13, 15 |
| 24 | [24-story-email-incoming-CRM-24.md](24-story-email-incoming-CRM-24.md) | Email: incoming email creates ticket | CRM-24 | 01–11, 23; Phase 2: 13, 15 |
| 25 | [25-story-whatsapp-send-replies-CRM-25.md](25-story-whatsapp-send-replies-CRM-25.md) | WhatsApp: send replies | CRM-25 | 01–11, 23, 26 (webhook); Phase 2: 13, 15 |
| 26 | [26-story-whatsapp-incoming-CRM-26.md](26-story-whatsapp-incoming-CRM-26.md) | WhatsApp: incoming message creates ticket | CRM-26 | 01–11, 24; Phase 2: 13, 15 |

## Dependency notes

- **Two phases.** Tickets (CRM-13, branch `feature/group-b-tickets`) and ticket replies / messages (CRM-15) are built in parallel by other groups. **Phase 1** (no ticket dependency) builds and tests the whole channel layer; **Phase 2** wires it to tickets after `main` contains the `CRM-15:` commits (`git merge main` into `feature/group-e-channels`). Each plan marks its tasks **[P1]** / **[P2]**.
- **Shared contracts created in this feature** (Phase 1):
  - Domain `Crm.Domain/Channels/`: `ChannelKind { Email, WhatsApp }`, `DeliveryStatus { Pending, Sent, Delivered, Read, Failed }`, **`OutboundMessage`** (outbound log / outbox with Failed + retry, CRM-23), **`ReceivedMessage`** (inbound log, unique `(Channel, ExternalId)` = Message-Id / WhatsApp message id, CRM-24), **`TicketNumberTag`** (`[TKT-000123]` in subjects), **`WhatsAppWindow`** (24-hour customer-service window, CRM-25).
  - Application `Crm.Application/Channels/`: **`IChannelProvider`** (every channel implements it — CLAUDE.md), `OutboundChannelMessage` / `ChannelSendResult`, **`IChannelSender`** (send + log + retry + delivery status), **`InboundChannelMessage`** (channel-neutral inbound message) + **`IInboundMessageProcessor`** (de-duplicate, match / create the customer, store; Phase 2: ticket), `ChannelText`, options POCOs `EmailChannelOptions` / `WhatsAppChannelOptions`, WhatsApp webhook signature + payload parsing + `IWhatsAppWebhookService`.
  - Infrastructure `Crm.Infrastructure/Channels/`: `SmtpEmailProvider` (MailKit, behind `ISmtpTransport`), `EmailMessageParser` (MimeKit → `InboundChannelMessage`), `EmailInboxPoller` (IMAP, behind `IImapMailbox`), `WhatsAppCloudClient` (typed `HttpClient`) + `WhatsAppChannelProvider`, repositories, migrations `AddOutboundMessages` (CRM-23) and `AddReceivedMessages` (CRM-24).
  - Api: `ChannelWorker` (hosted service: IMAP polling + retry of failed messages; not started in `Testing`; tests call the job methods directly), `/api/webhooks/whatsapp` (anonymous + signature-checked, CRM-7 "Webhooks"), `GET /api/channels/status` (`channels.manage`).
- **Configuration** (`Channels:Email:*`, `Channels:WhatsApp:*`): non-secret defaults may live in `appsettings.json`; passwords, access tokens, app secret and verify token only in user-secrets / environment variables. Missing settings never crash startup: the provider reports "not configured" and sends fail as `Failed` with that reason.
- **Customer matching** uses CRM-9 `ICustomerService.LookupAsync` / `CreateAsync` / `AddContactAsync` (see [../03-customer-management/09-story-customer-contacts-CRM-9.md](../03-customer-management/09-story-customer-contacts-CRM-9.md) section 6). Channel webhooks / jobs run without a user → timeline `ActorId` null ("System").
- **Hangfire** is not in the solution yet (the SLA stories may add it). Until then the recurring work runs in `ChannelWorker` (`PeriodicTimer`); the job classes are plain classes so they can move to Hangfire recurring jobs without change.
- **Not here:** SLA logic (CRM-19..22); ticket replies UI / `FirstResponseAt` (CRM-15) — Phase 2 only calls into them.
