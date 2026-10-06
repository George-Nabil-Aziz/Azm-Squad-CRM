# Story 26 — WhatsApp: incoming message creates ticket (Story: CRM-26)

## Prerequisites

- Story 24 completed ([24-story-email-incoming-CRM-24.md](24-story-email-incoming-CRM-24.md): `InboundChannelMessage`, `IInboundMessageProcessor`, `ReceivedMessage`, de-duplication).
- CRM-7 "Webhooks" ([../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md) ~line 674): `.AllowAnonymous()` + signature / verify-token check; `PermissionPolicyTests` skips anonymous endpoints.
- CRM-9 section 6: WhatsApp `wa_id` is digits without "+" → prefix "+" before `LookupAsync(new CustomerLookupQuery(phone, null))`; `ContactValues.TryNormalizePhone` first (unparseable → unknown).
- `UnauthorizedException` → 401 ProblemDetails, `ForbiddenException` → 403 (CRM-5 handler).
- **Phase 2 only:** CRM-13 / CRM-15 on `main`. No migration in Phase 1.

---

## Story Goal

1. **[P1]** `GET /api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=…&hub.challenge=…` → **200** `text/plain` with the challenge when the token equals `Channels:WhatsApp:VerifyToken` (AC 1); wrong / missing token or no verify token configured → **403**.
2. **[P1]** `POST /api/webhooks/whatsapp`: header `X-Hub-Signature-256: sha256=<hex HMAC-SHA256(raw body, AppSecret)>` checked in constant time; missing / wrong signature or no app secret configured → **401** (AC 2). Valid → **200** (always, also for payloads it does not understand, so Meta does not retry).
3. **[P1]** `entry[].changes[].value.messages[]` of type `text` (others → body "[<type> message]") become `InboundChannelMessage(WhatsApp, id, "+" + from, contacts[].profile.name, null, text.body, timestamp)` → `IInboundMessageProcessor`: duplicate message id ignored; known number → that customer; unknown → **new customer** (name = profile name or the number, primary phone + WhatsApp contact) (AC 4 customer part).
4. **[P2]** Known number: append to that customer's **open** ticket on WhatsApp (status not Resolved / Closed, newest), else create a ticket (`TicketChannel.WhatsApp`) (AC 3); unknown number → the new customer gets a ticket (AC 4). Timeline `messageReceived`.

---

## Context — Read These Files First

1. `.squad/plans/06-channels/00-overview.md`, plans 24 and 25.
2. `server/src/Crm.Api/Endpoints/HealthEndpoints.cs` (`AllowAnonymous` example), `CustomersEndpoints.cs`.
3. `server/tests/Crm.Api.IntegrationTests/Auth/PermissionPolicyTests.cs` lines 17–22 (anonymous endpoints are excluded).
4. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` lines 50–56 (in-memory config: add `Channels:WhatsApp:VerifyToken` / `AppSecret` **test** values).

---

## Backend Tasks

### 1 — Unit tests first (Red) [P1]

`server/tests/Crm.UnitTests/Channels/`:
- `WhatsAppSignatureTests` — valid signature true; wrong secret / tampered body / missing header / no "sha256=" prefix / empty secret → false; upper-case hex accepted.
- `WhatsAppWebhookParserTests` — text message (id, `+` number, profile name, body, UTC time), non-text type, several entries, invalid JSON → empty result (no exception).
- `WhatsAppWebhookServiceTests` — `Verify_WithMatchingToken_ReturnsChallenge` (AC 1), `Verify_WithWrongToken_ThrowsForbidden`, `Handle_WithInvalidSignature_ThrowsUnauthorized` (AC 2), `Handle_PassesMessagesToTheProcessor`.
- `InboundMessageProcessorTests` (extend) — `UnknownWhatsAppNumber_CreatesCustomer_WithWhatsAppContact` (AC 4), `KnownWhatsAppNumber_IsLinkedToThatCustomer` (AC 3).

### 2 — Application (Green) [P1]

- **Create** `server/src/Crm.Application/Channels/WhatsApp/WhatsAppSignature.cs`, `WhatsAppWebhookParser.cs` (`WhatsAppWebhookPayload(IReadOnlyList<InboundChannelMessage> Messages, IReadOnlyList<WhatsAppStatusUpdate> Statuses)`), `IWhatsAppWebhookService.cs` + `WhatsAppWebhookService.cs` (`string VerifySubscription(string? mode, string? token, string? challenge)`, `Task HandleAsync(byte[] body, string? signature, CancellationToken)`), `ChannelText.WebhookSignatureInvalid` / `WebhookVerifyTokenInvalid`.
- Processor: WhatsApp sender → phone lookup; new customer = `CreateAsync(new CustomerRequest(name, null, phone))` + `AddContactAsync(id, new CustomerContactRequest("whatsapp", phone, true))`.

### 3 — Api (Green) [P1]

- **Create** `server/src/Crm.Api/Endpoints/WhatsAppWebhookEndpoints.cs`: group `/api/webhooks/whatsapp` `.AllowAnonymous()`; `GET` reads `hub.mode`, `hub.verify_token`, `hub.challenge` (`[FromQuery(Name = "hub.challenge")]`) → `Results.Text(challenge)`; `POST` reads the raw body into a byte array (max 1 MB) and the header → `HandleAsync` → `Results.Ok()`. Map in `Program.cs`.

### 4 — Integration tests [P1]

`server/tests/Crm.Api.IntegrationTests/Channels/WhatsAppWebhookTests.cs` (signed with the factory's test secret):
- `Verify_WithTheRightToken_ReturnsTheChallenge` (AC 1), `Verify_WithAWrongToken_Returns403`.
- `Post_WithAnInvalidSignature_Returns401` / `Post_WithoutSignature_Returns401` (AC 2).
- `Post_FromAKnownNumber_LinksTheMessageToThatCustomer` (AC 3 customer part), `Post_FromAnUnknownNumber_CreatesACustomer` (AC 4 customer part: name, phone + WhatsApp contact), `Post_SameMessageTwice_StoresItOnce`.

### 5 — Ticket wiring [P2]

- Processor (shared with CRM-24): WhatsApp → newest ticket of the customer with `Channel = WhatsApp` and status not `Resolved` / `Closed` → append a customer message; none → create a ticket (subject = first 80 characters of the text). Tests: `KnownNumber_WithAnOpenTicket_AddsTheMessageToIt`, `KnownNumber_WithoutAnOpenTicket_CreatesATicket` (AC 3), `UnknownNumber_CreatesCustomerAndTicket` (AC 4).

---

## Frontend Tasks

No frontend changes.

---

## Edge Cases & Failure Modes

- **No `VerifyToken` / `AppSecret` configured** → GET 403 / POST 401 (never "accept everything").
- **Signature over the exact raw bytes** — the body is read once into a buffer before parsing; re-serialised JSON would not match.
- **Unknown payload shapes / statuses only / invalid JSON with valid signature** → 200, nothing stored.
- **Meta retries** the same message id → duplicate ignored (`ReceivedMessages` unique index).
- **Number in another country** (`"4479…"`) → "+" prefix makes it parse as E.164.
- **Profile name missing** → customer name = the number.
- **Large payloads** → body above 1 MB → 413 by Kestrel limit / explicit check.

---

## Test Plan

1. Unit: `WhatsAppSignatureTests`, `WhatsAppWebhookParserTests`, `WhatsAppWebhookServiceTests`, extended `InboundMessageProcessorTests`.
2. Integration: `WhatsAppWebhookTests`.
3. [P2] ticket tests above.

---

## Verification Steps

1. `server/`: `dotnet build` (0 warnings), `dotnet test`. 2. `client/`: `npm test`, `npm run build`, `npm run lint`.

---

## Done Criteria

- [ ] [P1] hub.challenge returned for the right token — AC 1.
- [ ] [P1] Invalid signature → 401 — AC 2.
- [ ] [P1] Known number matched; [P2] open ticket / new ticket — AC 3.
- [ ] [P1] Unknown number → new customer; [P2] + ticket — AC 4.
- [ ] All builds and tests green.
