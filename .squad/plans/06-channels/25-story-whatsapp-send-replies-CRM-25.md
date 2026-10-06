# Story 25 — WhatsApp: send replies (Story: CRM-25)

## Prerequisites

- Stories 23, 24 completed ([23-story-email-send-replies-CRM-23.md](23-story-email-send-replies-CRM-23.md): `IChannelProvider`, `OutboundMessage` incl. `ProviderMessageId` + `ApplyDeliveryStatus`, `IChannelSender`; [24-story-email-incoming-CRM-24.md](24-story-email-incoming-CRM-24.md): `ReceivedMessage`, `IReceivedMessageRepository.LastReceivedAtAsync`).
- Story 26 ([26-story-whatsapp-incoming-CRM-26.md](26-story-whatsapp-incoming-CRM-26.md)) provides the webhook endpoint and payload parser; this story adds the **status** part of the payload. Implementation order on the branch: 23 → 24 → 26 → 25.
- CRM-9: WhatsApp contacts are E.164 (`+9665…`); the Cloud API wants digits without "+".
- **Phase 2 only:** CRM-13 / CRM-15 on `main`.
- No new package (typed `HttpClient` + `System.Text.Json`), no migration.

---

## Story Goal

1. **[P1]** `WhatsAppCloudClient` (typed `HttpClient`, base `Channels:WhatsApp:ApiBaseUrl`, default `https://graph.facebook.com/v21.0/`) posts `/{PhoneNumberId}/messages` with `Authorization: Bearer {AccessToken}`: text `{ messaging_product: "whatsapp", to, type: "text", text: { body } }` or template `{ …, type: "template", template: { name, language: { code } } }`; returns the `messages[0].id` (wamid). `WhatsAppChannelProvider : IChannelProvider` (AC 1, AC 4).
2. **[P1]** **24-hour window** (`Crm.Domain.Channels.WhatsAppWindow.IsOpen(lastCustomerMessageAt, utcNow)`: open while `utcNow - last < 24 h`). `ChannelSender.SendAsync` for WhatsApp **without** a template looks up the customer's last inbound WhatsApp message (`IReceivedMessageRepository.LastReceivedAtAsync`) and, when the window is closed, throws `ValidationException { body: ["The last customer message is older than 24 hours. Send an approved template instead."] }` (400) — nothing is sent or logged (AC 2). With a template it is sent.
3. **[P1]** Status webhook: `statuses[]` entries (`id` = wamid, `status` = `sent` / `delivered` / `read` / `failed`, `timestamp`, `errors[0].title`) update the matching `OutboundMessage` via `IChannelSender.ApplyDeliveryStatusAsync` (AC 3). Status never moves backwards (`read` then a late `delivered` stays `read`); `failed` always applies.
4. **[P2]** An agent reply on a ticket with `Channel = WhatsApp` is sent through `IChannelSender` to the customer's primary WhatsApp contact (else primary phone); the reply form offers "Send template" (template name + language) when the API answers the window error; the reply shows its delivery status.

**Not in scope:** template management UI, media / interactive messages.

---

## Context — Read These Files First

1. `.squad/plans/06-channels/00-overview.md`, plans 23, 24, 26.
2. `server/src/Crm.Domain/Channels/OutboundMessage.cs`, `server/src/Crm.Application/Channels/ChannelSender.cs` (after story 23).
3. `server/src/Crm.Infrastructure/DependencyInjection.cs` (typed client registration with `AddHttpClient<WhatsAppCloudClient>`; `Microsoft.Extensions.Http` package if not already available).

---

## Backend Tasks

### 1 — Unit tests first (Red) [P1]

`server/tests/Crm.UnitTests/Channels/`:
- `WhatsAppWindowTests` — open at 23 h 59 min, closed at exactly 24 h and later, closed without any customer message, non-UTC throws.
- `ChannelSenderTests` (extend) — `WhatsApp_FreeTextInsideTheWindow_IsSent` (AC 1), `WhatsApp_FreeTextOutsideTheWindow_ThrowsValidation_OnBody` (AC 2), `WhatsApp_TemplateOutsideTheWindow_IsSent` (AC 2), `ApplyDeliveryStatus_UpdatesTheMessage` (AC 3), `ApplyDeliveryStatus_UnknownId_IsIgnored`.
- `OutboundMessageTests` (extend) — `ApplyDeliveryStatus_NeverGoesBackwards`, `ApplyDeliveryStatus_Failed_StoresTheError`.
- `WhatsAppWebhookParserTests` (extend from CRM-26) — statuses parsed (id, status, UTC time from the Unix `timestamp`, error title).

### 2 — Domain + Application (Green) [P1]

- **Create** `server/src/Crm.Domain/Channels/WhatsAppWindow.cs`.
- **Modify** `ChannelSender` (window rule, `ApplyDeliveryStatusAsync(string providerMessageId, DeliveryStatus, string? error, CancellationToken)`), `ChannelText.WhatsAppWindowClosed` / `WhatsAppNotConfigured`, `WhatsAppChannelOptions` (`PhoneNumberId`, `AccessToken`, `AppSecret`, `VerifyToken`, `ApiBaseUrl`, `TemplateLanguage = "en"`; `IsConfigured` = phone number id + access token).
- **Modify** `WhatsAppWebhookService.HandleAsync` (CRM-26) to apply the parsed statuses.

### 3 — Infrastructure (Green) [P1]

- **Create** `server/src/Crm.Infrastructure/Channels/WhatsApp/WhatsAppCloudClient.cs` (throws `WhatsAppApiException` with the Graph error message on a non-success status), `WhatsAppChannelProvider.cs` (not configured → `Fail(ChannelText.WhatsAppNotConfigured)`, API / network error → `Fail(message)`, recipient `+9665…` → `9665…`).
- DI: `services.AddHttpClient<WhatsAppCloudClient>()`; `IChannelProvider` → `WhatsAppChannelProvider`.

### 4 — Infrastructure / integration tests [P1]

`server/tests/Crm.Api.IntegrationTests/Channels/`:
- `WhatsAppCloudClientTests` (fake `HttpMessageHandler`, no network): text request (URL, bearer header, JSON body, `to` without "+"), template request, wamid returned, error status → exception with the Graph message.
- `WhatsAppChannelProviderTests`: not configured → failure; API error → failure.
- `WhatsAppStatusWebhookTests`: a stored `OutboundMessage` with `ProviderMessageId = "wamid.X"`; signed `POST /api/webhooks/whatsapp` with `delivered` then `read` → status `Read` (AC 3); `failed` with an error → `Failed` + `LastError`.

### 5 — Ticket wiring [P2]

- Reply use case of CRM-15: `ticket.Channel == TicketChannel.WhatsApp` → `IChannelSender.SendAsync(new ChannelReply(ChannelKind.WhatsApp, number, null, body, templateName, messageId))`; the window `ValidationException` surfaces as 400 to the reply form (reply **not** saved). Optional `templateName` on the reply request.
- Client: on the 400 `errors.body` window message show it and a "Send template" field; delivery status badge Sent / Delivered / Read / Failed (en + ar).
- Tests: `WhatsAppTicketReply_SendsThroughTheCloudApi` (AC 1), `WhatsAppTicketReply_OutsideTheWindow_Returns400` (AC 2), UI test for the template prompt.

---

## Frontend Tasks

- **[P1]** none. **[P2]** see 5.

---

## Edge Cases & Failure Modes

- **No inbound message ever** (agent starts the conversation) → window closed → template required.
- **Exactly 24 h** → closed (`<` not `<=`).
- **Graph API 4xx/5xx or timeout** → `Failed`, retried by the CRM-23 back-off; an expired token keeps failing until fixed (max 5 attempts).
- **Status for an unknown wamid** (sent outside the CRM) → ignored, webhook still 200.
- **Out-of-order statuses** → `ApplyDeliveryStatus` ranks Sent < Delivered < Read; `Failed` overrides.
- **Secrets** → `AccessToken` / `AppSecret` / `VerifyToken` only in user-secrets / env.

---

## Test Plan

1. Unit: `WhatsAppWindowTests`, extended `ChannelSenderTests`, `OutboundMessageTests`, `WhatsAppWebhookParserTests`.
2. Integration: `WhatsAppCloudClientTests`, `WhatsAppChannelProviderTests`, `WhatsAppStatusWebhookTests`.
3. [P2] reply tests.

---

## Verification Steps

1. `server/`: `dotnet build` (0 warnings), `dotnet test`. 2. `client/`: `npm test`, `npm run build`, `npm run lint`.

---

## Done Criteria

- [x] [P1] Cloud API client + provider; [P2] ticket reply sends — AC 1.
- [x] [P1] Window rule blocks free text, template allowed; [P2] agent told in the UI — AC 2.
- [x] [P1] Delivery status from the webhook — AC 3.
- [x] [P1] Through `IChannelProvider` — AC 4.
- [x] All builds and tests green.

## Phase 2 as built

- WhatsApp ticket reply → primary WhatsApp contact (else primary phone) through `ChannelTicketReplyDispatcher`. The 24-hour rule is checked **before the reply is saved**: `IChannelSender.EnsureCanSendAsync` (added to the interface; `SendAsync` uses it too) throws `ValidationException` on `body`, so the API answers 400 and nothing is stored. A `templateName` on the reply request sends the template.
- Webhook statuses update the `OutboundMessage` and, through `IChannelDeliveryObserver`, the `TicketMessage` (delivered / read → Sent, failed → Failed).
- Client: `TicketReplyForm` shows an "Approved WhatsApp template name" field after the server rejects the body, and sends `templateName`; delivery badge already existed from CRM-15 (`tickets.details.delivery.*`). Deviation: delivered / read are not shown separately (ticket delivery status is Pending / Sent / Failed).
- Tests: `WhatsAppTicketReply_*`, `WhatsAppStatusWebhook_Failed_*` (integration), client test for the template prompt.
- Developer must set (user-secrets / env): `Channels:WhatsApp:PhoneNumberId`, `AccessToken`, `AppSecret`, `VerifyToken` (optional `ApiBaseUrl`, `TemplateLanguage`).
