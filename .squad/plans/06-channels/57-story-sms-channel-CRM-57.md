# Story 57 — SMS channel (Story: CRM-57)

## Prerequisites

- Channels layer (CRM-23..26): `IChannelProvider`, `ChannelSender` (log + retry), `InboundMessageProcessor`, `ChannelTicketService`, `ChannelTicketReplyDispatcher`; WhatsApp as the model (`Crm.Infrastructure/Channels/WhatsApp/*`, `Crm.Application/Channels/WhatsApp/*`, `Crm.Api/Endpoints/WhatsAppWebhookEndpoints.cs`).
- Spec ideas from CRM 01 `specs/14-contact-sms`: signature check → 401, unknown sender creates a customer, provider retries (same message id) are de-duplicated, delivery-failure callbacks are handled.
- No migration (channel enums are stored as strings). `ChannelKind.Sms`, `TicketChannel.Sms = 7`.

---

## Story Goal

1. SMS replies on an SMS ticket go through `SmsChannelProvider : IChannelProvider` (typed `HttpClient` to a Twilio-compatible API, configured from `Channels:Sms`) via `IChannelSender` (AC 1).
2. `POST /api/webhooks/sms` (anonymous, Twilio form post, `X-Twilio-Signature` = base64 HMAC-SHA1 of URL + sorted params with the auth token; wrong / missing → 401) turns an incoming SMS into an `InboundChannelMessage`: known number → that customer, unknown → new customer; added to the customer's open SMS ticket or a new ticket (`TicketChannel.Sms`); duplicates (`MessageSid`) are ignored (AC 2).
3. `POST /api/webhooks/sms/status` applies provider delivery statuses (`delivered`, `undelivered` / `failed` → Failed). A failed send is stored `Failed` and retried with the existing back-off (AC 3).
4. `SmsSegments` (GSM-7: 160 / 153 per segment, extension characters count 2; otherwise UCS-2: 70 / 67) tells how many segments a text needs; the ticket reply box warns the agent when it is more than one (AC 4). `POST /api/channels/sms/segments` exposes the same calculation (`tickets.manage`).

---

## Backend Tasks

### 1 — Tests first

Unit (`server/tests/Crm.UnitTests/Channels/`): `SmsSegmentsTests` (GSM-7 160 → 1, 161 → 2, 306 → 2, 307 → 3, extension chars count double, Arabic 70 → 1, 71 → 2, 134 → 2, 135 → 3, emoji = 2 units, empty → 0), `TwilioSignatureTests` (valid; wrong token / tampered param / missing / empty token → false), `SmsWebhookServiceTests` (invalid signature → Unauthorized; valid → processor gets the message with `+` number; status `delivered` / `failed` → `ApplyDeliveryStatusAsync`), `InboundMessageProcessorTests` (unknown SMS number creates a customer), `ChannelTicketServiceTests` (SMS adds to the open SMS ticket / creates one), `ChannelTicketReplyDispatcherTests` (SMS ticket → phone number, `ChannelKind.Sms`), `ChannelSenderTests` (SMS not configured → Failed "not configured").
Integration (`server/tests/Crm.Api.IntegrationTests/Channels/`): `SmsCloudClientTests` (stub handler: request URL, basic auth, form fields; success → provider id; 4xx → failure text; exception → failure), `SmsWebhookTests` (signed with the factory test token: 401 bad signature, creates customer + ticket, same sid twice → one message, second message joins the open ticket; status callback), `SmsSegmentsEndpointTests`.

### 2 — Domain / Application

`ChannelKind.Sms`, `TicketChannel.Sms`; `Crm.Domain/Channels/SmsSegments`; `SmsChannelOptions` (`AccountSid`, `AuthToken`, `FromNumber`, `ApiBaseUrl`, `WebhookUrl`); `Crm.Application/Channels/Sms/` (`TwilioSignature`, `SmsWebhookParser` for the form fields, `ISmsWebhookService` + service); `ChannelValues`, `ChannelText` (not configured, SMS subject), `ChannelStatusResponse.Sms`, `ChannelSender` (not-configured text per channel, status), `InboundMessageProcessor` (phone lookup / create for SMS), `ChannelTicketService` (SMS like WhatsApp), `ChannelTicketReplyDispatcher` (SMS branch), `ChannelSettingsApplier`-free (credentials only from config / user-secrets / env).

### 3 — Infrastructure / Api

`SmsChannelProvider` + `TwilioSmsClient` (`AddHttpClient`), registered in `AddChannels()`; `Crm.Api/Endpoints/SmsWebhookEndpoints.cs` (`/api/webhooks/sms`, `/api/webhooks/sms/status`, anonymous, form body, 1 MB cap) and `POST /api/channels/sms/segments` in `ChannelsEndpoints`.

## Frontend Tasks

Tests first: `lib/sms-segments.test.ts` (same vectors), `features/tickets/TicketReplyForm.sms.test.tsx` (warning shown above one segment, hidden for email tickets). `lib/sms-segments.ts`, `TicketReplyForm` gets `channel` and shows an alert; `ticketChannels` gains `webform`, `chat`, `sms` (i18n `tickets.channels.*`, previously missing for web form and chat); i18n `tickets.details.smsSegments`.

## Edge Cases & Failure Modes

- No credentials → `IsConfigured` false, sends fail as "SMS is not configured", webhook answers 401 (never accepts unsigned); startup unaffected.
- Behind a proxy the signed URL differs: set `Channels:Sms:WebhookUrl` to the public URL the provider calls.
- Numbers are normalised to E.164 by the customer rules; unusable senders keep the message without a customer.
- Delivery status never moves backwards (reuses `OutboundMessage.ApplyDeliveryStatus`).
- Only text SMS (no MMS).

## Verification / Done

`dotnet build` (0 warnings), filtered tests, then full suites once at the end. - [x] AC 1 - [x] AC 2 - [x] AC 3 - [x] AC 4

## As built

- Deviation: webhook URL for the signature = `Channels:Sms:WebhookBaseUrl` + path (else the request URL); status callback URL is sent with every message.
- Client: segment calculation is mirrored in `lib/sms-segments.ts` (same test vectors as the server); `POST /api/channels/sms/segments` serves API consumers.
- Also fixed: `ticketChannels` / i18n now include `webform`, `chat`, `sms`; report / nav tests list the new channels.
