# Story 58 — Public REST API + API keys (Story: CRM-58)

## Prerequisites

- CRM-7 (permission catalogue), CRM-8 (customers), CRM-13/14 (tickets), CRM-5 (ProblemDetails). Branch `feature/phase3-group-v`. Reference only: CRM 01 `specs/49-apis`.
- New package: `Swashbuckle.AspNetCore.SwaggerUI` (Api). Migration `AddApiKeys`.

## Story Goal

An admin creates scoped API keys; external systems call `/api/v1/*` with header `X-Api-Key`.

1. **AC 1 — generate, shown once, stored hashed.** `POST /api/api-keys {name, scopes[]}` (`integrations.manage`) returns `key` (`crm_` + 32 random bytes base64url) exactly once; the table stores only `KeyPrefix` (first 8 chars, for display) and `KeyHash` (SHA-256 hex). `GET /api/api-keys` never returns the key. Blank name / no scope / unknown scope → 400 with field errors. Scopes: `tickets:read`, `tickets:write`, `customers:read`, `customers:write`.
2. **AC 2 — scopes.** `GET /api/v1/tickets`, `GET /api/v1/tickets/{id}` need `tickets:read`; `POST /api/v1/tickets` needs `tickets:write`; same for customers. Wrong scope → 403, missing / unknown key → 401. Request / response shapes reuse the staff DTOs.
3. **AC 3 — revoke.** `DELETE /api/api-keys/{id}` sets `RevokedAt`; a revoked key → 401 on the next request.
4. **AC 4 — rate limit.** ASP.NET `AddRateLimiter` fixed window per key (partition = the header value, else IP), limit `Integrations:Api:RequestsPerMinute` (default 60); over the limit → 429 ProblemDetails with `Retry-After`.
5. **AC 5 — Swagger.** A second OpenAPI document `public-v1` (only `/api/v1/*`, `X-Api-Key` security scheme) at `/openapi/public-v1.json`, Swagger UI at `/swagger` (all environments; it documents the public API only).

**Decisions**

- API keys are checked by an endpoint filter (`ApiKeyScopeFilter`) calling `IApiKeyService.AuthenticateAsync`, not a JWT scheme. Calls run without a staff user (`ICurrentUser.UserId` null, actor "System").
- `LastUsedAt` is updated on each authenticated call.

## Tasks (tests first)

**T1 — Tests (Red):** unit `ApiKeyTests` (generation format, hash, prefix, scope parsing), `ApiKeyServiceTests` (create validation, authenticate ok / unknown / revoked / scope), permission catalogue tests (`integrations.manage`); integration `ApiKeysApiTests` (shown once, DB holds no plain key, list hides key, 400, Agent 403, 401), `PublicApiTests` (read ok with tickets:read, create 403, create ok with write, customers, revoked 401, no key 401, 429 with limit 3, openapi doc + swagger UI); client `api/integrations.test.ts`, `ApiKeysPage.test.tsx`, nav / route tests.

**T2 — Domain/Application:** `Domain/Integrations/ApiKey.cs` (+ `ApiKeyScopes`), `Application/Integrations/ApiKeys.cs` (DTOs, validator, repository interface, `IApiKeyService`), permission + role mapping.

**T3 — Infrastructure/Api:** configuration + repository, migration `AddApiKeys`, `ApiKeysEndpoints`, `PublicApiEndpoints` (+ filter), rate limiter, OpenAPI document + Swagger UI.

**T4 — Client:** `api/integrations.ts`, `pages/integrations/ApiKeysPage.tsx` (create with the one-time key, list, revoke), `/settings/integrations/*` routes + navigation, permissions mirror, i18n en/ar.

**T5 — Verify.**

## Edge cases

- Key header with the wrong prefix / empty → 401 without a DB hit.
- Out of scope: IP allow-lists, key expiry, update / delete endpoints in v1.
