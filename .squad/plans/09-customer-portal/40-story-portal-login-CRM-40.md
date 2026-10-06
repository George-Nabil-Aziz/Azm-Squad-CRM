# Story 40 — Customer portal: login (Story: CRM-40)

## Prerequisites

- Stories 01–11 (JWT auth CRM-2, customers CRM-8/9), 23 (email channel + `IChannelSender`); knowledge base stories 36–39 done (shared `PortalOptions`, `PortalKbEndpoints`).
- CRM 01 reference (read-only): `specs/35-submit-tickets-portal/`.

## Story Goal

1. A customer enters an **email**; the API emails a **6-digit one-time code**; the correct code **signs the customer in** (access token) (AC 1). Requesting a code answers 204 whether or not the email is known (no enumeration) and is throttled (one code per email per minute).
2. A **wrong code returns 401**; a code **expires after 10 minutes** (`TimeProvider`), works once, and 5 wrong tries burn it (AC 2).
3. A **customer token calling staff APIs gets 403** (AC 3); a staff token on a portal endpoint gets 403 too.
4. The **portal account is linked to the existing customer with the same email** (primary or secondary email contact); an unknown email creates a customer (name = the part before "@") and links it (AC 4).
5. Client: `/portal` area with its own layout, the sign-in page (email step, code step), its own session kept apart from the staff session.

## Design

- **Domain** `Crm.Domain/Portal/`: `PortalAccount` (CustomerId, Email, CreatedAt, LastLoginAt), `PortalLoginCode` (Email, CodeHash, CreatedAt, ExpiresAt, Attempts, ConsumedAt; `Verify(hash, now)` → `Ok | Wrong | Expired | Used | TooManyAttempts`; lifetime 10 minutes, 5 attempts).
- **Application** `Crm.Application/Portal/`: `IPortalAuthService` (`RequestCodeAsync`, `VerifyAsync`), request / response records and validators, `PortalText`, `IPortalCodeGenerator` (random 6 digits, replaced by a fake in tests), `PortalCodeHash` (SHA-256 of `email:code`, compared in constant time), `IPortalAccountRepository`, `PortalRoles.Customer = "Customer"`. Customer lookup through `ICustomerRepository.FindByContactAsync(Email, …)`; emails go through `IChannelSender` (channel Email, logged and retried; the code is part of the stored message body: it is single-use and valid for 10 minutes).
- **Token:** the existing `IAccessTokenGenerator` with role `Customer` and `sub` = customer id. `RolePermissions` knows no permission for it, so every staff policy answers 403. Policy `Portal` (`RequireRole(Customer)`) guards the portal APIs. The bearer handler's "still active" check verifies that the customer exists (not deleted) for `Customer` tokens, and the user check for staff tokens.
- **API** `PortalAuthEndpoints.cs`: `POST /api/portal/auth/request-code` (anonymous, 204), `POST /api/portal/auth/verify` (anonymous, 200 `{ accessToken, tokenType, expiresAt, customer }`), `GET /api/portal/auth/me` (`Portal`).
- **Migration** `AddPortalAuth` (`PortalAccounts` unique by email, `PortalLoginCodes`).

## Backend Tasks

1. Tests first: unit `PortalLoginCodeTests` (ok, wrong, expiry at 10 min boundary, used once, 5 attempts), `PortalAuthServiceTests` (code mailed through a fake sender, correct code → token for the linked customer, existing customer by email linked, unknown email creates a customer, wrong code → `UnauthorizedException`, expired → 401, second use → 401, throttle, invalid email → validation), integration `PortalLoginTests` (request → code read from the fake email provider → verify → token; wrong code 401; expiry with the fake clock; unknown email works; existing customer is linked; customer token on staff endpoints 403 for every staff endpoint; staff token on `/api/portal/auth/me` 403; deleted customer's token 401).
2. Implement domain, application, infrastructure (repository + configurations + migration), API, authentication changes (`Portal` policy, token validation for customers).

## Frontend Tasks

1. Tests first: `PortalLoginPage.test.tsx` (enter email → code step → success stores the portal session and goes to `/portal`; wrong code shows the error; resend; validation of the email), `portal-session.test.ts`, route test (staff pages unaffected; `/portal/*` redirects to the sign-in page when signed out).
2. `auth/portal-session.ts` (separate storage key), `api/portal-auth.ts`, `api/client.ts` sends the portal token on `/api/portal/` calls and the staff token elsewhere, `components/portal/PortalLayout.tsx` (header with language switcher, links, sign out), `pages/portal/PortalLoginPage.tsx`, `RequirePortalAuth`, routes under `/portal`, i18n `portal.*`.

## Edge cases

- Two customers with the same email: the first by name is linked (documented). Email is compared lower-case. A new code invalidates older ones. A token of a customer deleted later gets 401.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.

## As built

- Migration `AddPortalAuth`. Deviation: a repeated code request within a minute silently sends nothing (the earlier code keeps working). The portal client token is chosen by path prefix `/api/portal/` in `api/client.ts`. `/portal` pages (layout, home) are public; protected portal routes use `RequirePortalAuth` (used from CRM-41).
