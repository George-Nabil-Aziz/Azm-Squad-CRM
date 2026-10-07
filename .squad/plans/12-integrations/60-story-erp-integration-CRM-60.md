# Story 60 — ERP integration (Story: CRM-60)

## Prerequisites

- CRM-58 (`integrations.manage`, Integrations client area), CRM-8/9 (customers), CRM-30 (customer panel). Reference only: CRM 01 `specs/50-erp-integration`. Migration `AddErpIntegration`.
- The ERP is not confirmed: `IErpClient` + a generic REST `HttpErpClient` (`GET {BaseUrl}/customers/{erpId}/orders?limit=` and `/invoices?limit=`, header `Authorization: Bearer <ApiKey>`), configured by `Integrations:Erp:BaseUrl`, `Integrations:Erp:ApiKey`, `Integrations:Erp:TimeoutSeconds` (user-secrets / environment). Missing configuration = "not configured", never a startup failure.

## Story Goal

1. **AC 1 — link.** `Customer.ErpCustomerId` (nullable, max 100, unique among customers when set). `PUT /api/customers/{id}/erp-link {erpCustomerId}` (`customers.manage`; empty / null unlinks); an id already linked to another customer → 409.
2. **AC 2 — read-only data.** `GET /api/customers/{id}/erp` (`customers.view`) returns `{linked, erpCustomerId, available, message, orders[], invoices[], fetchedAt}` (last 5 of each); the customer details page shows an "ERP" section (orders and invoices tables, no edit actions).
3. **AC 3 — ERP down.** Timeout / error / not configured → still 200 with `available=false` and a localized message; the customer page loads and shows the message.
4. **AC 4 — sync log.** Every fetch writes an `ErpSyncLog` row (customer, ERP id, result `success | failed | not_configured`, error text, UTC time); `GET /api/integrations/erp/sync-logs` (`integrations.manage`, paged) and a client page list them.

## Tasks (tests first)

**T1 — Tests (Red):** unit `ErpServiceTests` (not linked, success, ERP down returns unavailable + logs failed, not configured logs, link conflict 409, unlink), `HttpErpClientTests` (stub handler: parse orders / invoices, 500 / timeout → failure); integration `ErpApiTests` (link, 409, view with fake `IErpClient`, down, logs endpoint, 403 / 401); client `api` test, `ErpPanel.test.tsx` (data, down message), logs page test.

**T2 — Domain/Application:** `Customer.LinkErp`, `ErpContracts`, `IErpClient`, `IErpSyncLogRepository`, `IErpService`, text.

**T3 — Infrastructure/Api:** `HttpErpClient`, repositories, configuration + migration, endpoints.

**T4 — Client:** `ErpPanel` on the customer details page, link control, `pages/integrations/ErpLogsPage`, i18n.

**T5 — Verify.**

## Out of scope

Writing back to the ERP, scheduled bulk sync, caching, a specific ERP product mapping.
