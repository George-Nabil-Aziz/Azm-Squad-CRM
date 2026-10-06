# Story 55 — Web forms (Story: CRM-55)

## Prerequisites

- Channels layer from CRM-23..26 (`IChannelSender`, `ChannelReply`, `TicketNumberTag`), tickets (CRM-13: `ITicketService.CreateForCustomerAsync`), customers (CRM-9: `ICustomerService.LookupAsync` / `CreateAsync`).
- Precedent for "customer opens a ticket without staff": [../09-customer-portal/41-story-portal-submit-ticket-CRM-41.md](../09-customer-portal/41-story-portal-submit-ticket-CRM-41.md) (`PortalTicketService`: create ticket, confirmation email).
- No migration: `Ticket.Channel` is stored as a string (`TicketConfiguration`), the new value `WebForm` fits.

---

## Story Goal

1. `POST /api/public/web-forms` (anonymous) with `name`, `email`, `subject`, `message`, `captchaToken` creates a ticket with `TicketChannel.WebForm` for the matched (by email) or new customer (AC 1).
2. Captcha is verified through `ICaptchaVerifier` (HTTP, Cloudflare Turnstile / reCAPTCHA-compatible `siteverify`); a per-IP fixed-window rate limit (`WebForms:RateLimitRequests` per `RateLimitWindowSeconds`, default 5 / 60 s) answers **429** with `Retry-After` (AC 2). A wrong captcha is a 400 on `captchaToken`.
3. Missing / invalid `name`, `email`, `subject`, `message` → **400** with field errors (AC 3).
4. The submitter gets a confirmation email with the ticket number (`[TKT-n]` tag in the subject so replies land on the ticket) (AC 4). A mail failure never fails the submit (the channel layer logs and retries it).
5. Staff replies on a WebForm ticket go to the customer by email (dispatcher maps WebForm → email).
6. Embedding: public SPA page `/embed/contact` (iframe-able form) + admin page `/settings/web-forms` (permission `channels.manage`) that shows the iframe snippet.

---

## Context — Read These Files First

1. `server/src/Crm.Application/Portal/PortalTicketService.cs` (create + confirmation pattern), `Tickets/ChannelTicketReplyDispatcher.cs`, `Reports/DashboardService.cs` line ~59 (`Enumerable.Range(1, 4)` over `TicketChannel`).
2. `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs`, `Crm.Api/Endpoints/WhatsAppWebhookEndpoints.cs` (anonymous endpoint pattern), `Crm.Api/Auth/HttpClientInfo.cs` (`IClientInfo.IpAddress`).
3. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` (`WithWebHostBuilder` to change config / replace services), `Portal/PortalSubmitTicketTests.cs`.
4. Client: `src/app/AppRoutes.tsx`, `src/app/navigation.ts`, `src/api/client.ts`, `src/pages/portal/PortalNewTicketPage.tsx` (form pattern).

---

## Backend Tasks

### 1 — Unit tests first (Red)

`server/tests/Crm.UnitTests/WebForms/`:
- `FixedWindowRateLimiterTests` — allows `limit` calls, the next is refused with a positive `retryAfter`; a new window (fake time) allows again; keys are independent.
- `WebFormRequestValidatorTests` — each required field empty → error on that field; bad email; too long values (AC 3).
- `WebFormServiceTests` (fakes) — `Submit_CreatesAWebFormTicket_ForANewCustomer` (AC 1), `Submit_ReusesTheCustomerWithTheSameEmail`, `Submit_RateLimited_Throws429Exception_AndCreatesNothing` (AC 2), `Submit_WithAFailedCaptcha_ThrowsValidationOnCaptchaToken` (AC 2), `Submit_Sends_AConfirmationEmail_WithTheTicketTag` (AC 4), `Submit_WhenMailFails_StillSucceeds`, `Submit_WithTheHoneypotFilled_CreatesNothing`.
- `ChannelTicketReplyDispatcherTests` (extend) — `WebFormTicket_RepliesByEmail`.

### 2 — Domain / Application (Green)

- `Crm.Domain/Tickets/TicketChannel.cs`: add `WebForm = 5`; fix `DashboardService` to use `Enum.GetValues<TicketChannel>()`.
- `Crm.Application/Common/RateLimiting/`: `IRateLimiter` (`bool TryAcquire(string key, int limit, TimeSpan window, out TimeSpan retryAfter)`), `FixedWindowRateLimiter` (singleton, `TimeProvider`, `ConcurrentDictionary`; expired entries pruned), `RateLimitExceededException(TimeSpan RetryAfter)`. Reused by CRM-56 and CRM-58.
- `Crm.Application/WebForms/`: `WebFormOptions` (section `WebForms`: `Enabled`, `RateLimitRequests`, `RateLimitWindowSeconds`, `CaptchaSecret`, `CaptchaSiteKey`, `CaptchaVerifyUrl`), `ICaptchaVerifier`, `WebFormRequest`/`WebFormReceipt` + validator, `IWebFormService` + `WebFormService`, `WebFormText` (en/ar), `WebFormCaptchaConfig` response.
- `Crm.Application/Tickets/ChannelTicketReplyDispatcher.cs`: WebForm → same branch as Email.
- Registration: `AddWebForms()` in `Crm.Application` (`DependencyInjection.AddApplication`).

### 3 — Infrastructure (Green)

- `Crm.Infrastructure/WebForms/HttpCaptchaVerifier.cs` (typed `HttpClient`, form-post `secret`, `response`, `remoteip`; `success` flag; not configured → verification skipped, documented). Registered by `AddWebFormsInfrastructure()`.
- Tests: `HttpCaptchaVerifierTests` with an `HttpMessageHandler` stub (success, failure, HTTP error → false, not configured).

### 4 — Api

- `Crm.Api/ErrorHandling/GlobalExceptionHandler.cs`: `RateLimitExceededException` → 429 ProblemDetails + `Retry-After` header.
- `Crm.Api/Endpoints/WebFormsEndpoints.cs`: `MapWebFormsEndpoints()`: group `/api/public/web-forms` `.AllowAnonymous()`: `GET config` (captcha site key, `captchaRequired`), `POST ""` → 201 `{ number }`. Map in `Program.cs`.

### 5 — Integration tests

`server/tests/Crm.Api.IntegrationTests/WebForms/WebFormTests.cs` (host with a fake `ICaptchaVerifier` and a recording email provider): `Post_CreatesATicket_WithChannelWebForm` (AC 1), `Post_BeyondTheLimit_Returns429_WithRetryAfter` (AC 2), `Post_WithAFailedCaptcha_Returns400`, `Post_MissingFields_Returns400_WithFieldErrors` (AC 3), `Post_SendsAConfirmationWithTheTicketNumber` (AC 4), `Config_IsAnonymous`.

---

## Frontend Tasks

- Tests first: `src/api/web-forms.test.ts`, `src/pages/public/ContactFormPage.test.tsx` (required-field errors, successful submit shows the ticket number, 429 message), `src/pages/settings/WebFormsPage.test.tsx` (snippet contains the embed URL).
- `src/api/web-forms.ts` (typed client), `src/pages/public/ContactFormPage.tsx` (route `/embed/contact`, outside the app layout, no auth; react-hook-form + zod; optional captcha widget when a site key is configured), `src/features/web-forms/CaptchaWidget.tsx`, `src/pages/settings/WebFormsPage.tsx` (route `/settings/web-forms`, `RequirePermission channelsManage`, nav entry), i18n keys in `ar.json` / `en.json`.

---

## Edge Cases & Failure Modes

- **No captcha secret configured** → captcha is skipped (rate limit + honeypot still apply); it must be set in production (`WebForms__CaptchaSecret`).
- **Captcha provider down** → treated as a failed captcha (400), never an accepted submit.
- **Rate limit key** = remote IP (no `X-Forwarded-For` trust, same as `HttpClientInfo`); behind a proxy configure forwarded headers.
- **Honeypot field `website` filled** → 201 without creating anything (bots get no signal).
- **Email matches a customer** → reused; the submitter never sees customer data.
- **Mail not configured** → confirmation is logged as Failed and retried; ticket still created.
- **Uncertainty:** the embed page is an iframe of the SPA (no CORS needed); a script-tag widget is out of scope.

---

## Test Plan

Unit (rate limiter, validator, service, dispatcher), integration (`WebFormTests`, `HttpCaptchaVerifierTests`), client tests above. `dotnet test`, `npm test`.

## Verification Steps

1. `server/`: `dotnet build` (0 warnings), `dotnet test`. 2. `client/`: `npm test`, `npm run build`, `npm run lint`.

## Done Criteria

- [ ] AC 1 ticket with channel WebForm. - [ ] AC 2 captcha + 429. - [ ] AC 3 400 on missing fields. - [ ] AC 4 confirmation email with number. - [ ] All builds and tests green.
