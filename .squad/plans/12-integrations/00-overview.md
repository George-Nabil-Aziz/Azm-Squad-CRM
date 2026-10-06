# integrations — plan overview

Entry point for the **integrations** feature (Phase 3): a public REST API secured with API keys, outgoing webhooks for CRM events and a read-only ERP integration. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 58 | [58-story-public-api-keys-CRM-58.md](58-story-public-api-keys-CRM-58.md) | Public REST API + API keys | CRM-58 | 01–13, 55 (`IRateLimiter`) |
| 59 | [59-story-outgoing-webhooks-CRM-59.md](59-story-outgoing-webhooks-CRM-59.md) | Outgoing webhooks | CRM-59 | 01–17, 35 (`ISecretProtector`) |
| 60 | [60-story-erp-integration-CRM-60.md](60-story-erp-integration-CRM-60.md) | ERP integration | CRM-60 | 01–11, 30 (customer panel) |

## Notes

- Every external call sits behind an interface with an `HttpClient` implementation configured from settings; tests use `HttpMessageHandler` stubs / fakes. Missing settings never crash startup.
- New permissions (`apikeys.manage`, `webhooks.manage`, `erp.link`) are added to `Permissions.cs`, `RolePermissions.cs` (Admin + SuperAdmin) and mirrored in `client/src/auth/permissions.ts`.
- Secrets (webhook signing secrets) are encrypted with `ISecretProtector` (CRM-35); API keys are stored as SHA-256 hashes and shown once.
