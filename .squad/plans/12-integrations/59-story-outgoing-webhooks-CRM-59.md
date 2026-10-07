# Story 59 — Outgoing webhooks (Story: CRM-59)

## Prerequisites

- CRM-58 (permission `integrations.manage`, Integrations client area), CRM-13/17 (ticket create / resolve), CRM-35 (`ISecretProtector`). Reference only: CRM 01 `specs/52-external-systems`. Migration `AddWebhooks`.

## Story Goal

Admins register URLs for CRM events; the CRM POSTs signed JSON to them reliably.

1. **AC 1 — register.** `POST /api/webhooks {name, url, events[]}` (`integrations.manage`); events `ticket.created`, `ticket.resolved`; url must be absolute http(s); at least one known event; 400 with field errors. The signing secret (`whsec_...`) is generated and shown once; stored encrypted (`ISecretProtector`). `GET /api/webhooks`, `PUT /api/webhooks/{id}`, `DELETE /api/webhooks/{id}`.
2. **AC 2 — HMAC.** Each delivery POSTs `{id, event, occurredAt, data}` with headers `X-Crm-Event`, `X-Crm-Delivery` and `X-Crm-Signature: sha256=<hex HMAC-SHA256(secret, raw body)>`.
3. **AC 3 — retry + log.** Events go to an outbox (`WebhookDelivery`: Pending / Delivered / Failed, attempts, next attempt, last status / error). `WebhookDeliveryJob.RunAsync` (plain class, run by `ChannelWorker`) sends due deliveries; non-2xx / exception → retry with backoff 1, 5, 30, 120, 360 minutes, then Failed. `GET /api/webhooks/{id}/deliveries` shows the log. Publishing never fails the ticket operation.
4. **AC 4 — disable.** `POST /api/webhooks/{id}/disable|enable`; a disabled webhook gets no new deliveries and its pending ones are cancelled (marked Failed "webhook disabled") without sending.

**Decisions**

- `IWebhookEventPublisher` (Application) is an optional dependency of `TicketService` (created), `ChannelTicketService` (created) and `TicketStatusService` (resolved); the HTTP call sits behind `IWebhookSender` (`HttpWebhookSender`, 10 s timeout) so tests use a fake.
- No SSRF filtering of private addresses (admins are trusted); noted as a limitation.

## Tasks (tests first)

**T1 — Tests (Red):** unit `WebhookSignatureTests`, `WebhookBackoffTests`, `WebhookDeliveryJobTests` (success, retry schedule with fake clock, gives up after 5, disabled webhooks not sent, signature verifiable), `WebhookServiceTests` (validation, secret shown once); integration `WebhooksApiTests` (CRUD + 400/401/403, creating and resolving a ticket enqueue deliveries for subscribed webhooks only, disabled none, deliveries list, job run with a fake sender); client `api` + `WebhooksPage.test.tsx`.

**T2 — Domain/Application:** `Webhook`, `WebhookDelivery`, `WebhookEvents`, `WebhookSignature`, `WebhookBackoff`, DTOs, validator, `IWebhookService`, `IWebhookEventPublisher`, `WebhookDeliveryJob`, `IWebhookSender`.

**T3 — Infrastructure/Api:** configurations + migration, repository, `HttpWebhookSender`, `WebhooksEndpoints`, `ChannelWorker` job, ticket hooks.

**T4 — Client:** `WebhooksPage` (register with one-time secret, list with enable / disable, delivery log), navigation item, i18n.

**T5 — Verify.**

## Out of scope

Customer events, manual redelivery, SSRF allow-lists, per-webhook headers.
