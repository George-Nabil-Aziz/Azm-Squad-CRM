import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { PagedResult } from './paging'

/** Scopes of an API key (server: Crm.Domain.Integrations.ApiKeyScopes). */
export const apiKeyScopes = ['tickets:read', 'tickets:write', 'customers:read', 'customers:write'] as const

export type ApiKeyScope = (typeof apiKeyScopes)[number]

/** An API key as listed (server: ApiKeyResponse); the key itself is never returned again. */
export interface ApiKey {
  id: string
  name: string
  keyPrefix: string
  scopes: ApiKeyScope[]
  createdAt: string
  lastUsedAt: string | null
  revokedAt: string | null
}

/** Response of creating a key: `key` is shown only this once. */
export interface CreatedApiKey extends Omit<ApiKey, 'lastUsedAt' | 'revokedAt'> {
  key: string
}

export function listApiKeys(signal?: AbortSignal): Promise<ApiKey[]> {
  return apiGet<ApiKey[]>('/api/api-keys', signal)
}

export function createApiKey(request: { name: string; scopes: ApiKeyScope[] }): Promise<CreatedApiKey> {
  return apiPost<CreatedApiKey>('/api/api-keys', request)
}

export function revokeApiKey(id: string): Promise<void> {
  return apiDelete(`/api/api-keys/${encodeURIComponent(id)}`)
}

/** CRM events a webhook can subscribe to (server: Crm.Domain.Integrations.WebhookEvents). */
export const webhookEvents = ['ticket.created', 'ticket.resolved'] as const

export type WebhookEvent = (typeof webhookEvents)[number]

/** A webhook as listed (server: WebhookResponse); the signing secret is never returned again. */
export interface Webhook {
  id: string
  name: string
  url: string
  events: WebhookEvent[]
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

/** Response of registering a webhook: `secret` (signs every delivery) is shown only this once. */
export interface CreatedWebhook extends Webhook {
  secret: string
}

export interface WebhookRequest {
  name: string
  url: string
  events: WebhookEvent[]
}

/** One row of a webhook's delivery log. */
export interface WebhookDelivery {
  id: string
  event: WebhookEvent
  status: 'pending' | 'delivered' | 'failed'
  attempts: number
  nextAttemptAt: string
  lastStatusCode: number | null
  lastError: string | null
  createdAt: string
  deliveredAt: string | null
}

export function listWebhooks(signal?: AbortSignal): Promise<Webhook[]> {
  return apiGet<Webhook[]>('/api/webhooks', signal)
}

export function createWebhook(request: WebhookRequest): Promise<CreatedWebhook> {
  return apiPost<CreatedWebhook>('/api/webhooks', request)
}

export function setWebhookEnabled(id: string, enabled: boolean): Promise<Webhook> {
  return apiPost<Webhook>(`/api/webhooks/${encodeURIComponent(id)}/${enabled ? 'enable' : 'disable'}`, {})
}

export function deleteWebhook(id: string): Promise<void> {
  return apiDelete(`/api/webhooks/${encodeURIComponent(id)}`)
}

export function listWebhookDeliveries(id: string, signal?: AbortSignal): Promise<WebhookDelivery[]> {
  return apiGet<WebhookDelivery[]>(`/api/webhooks/${encodeURIComponent(id)}/deliveries`, signal)
}

/** An order of the customer in the ERP (server: ErpOrder). */
export interface ErpOrder {
  id: string
  number: string | null
  date: string | null
  status: string | null
  total: number | null
  currency: string | null
}

/** An invoice of the customer in the ERP (server: ErpInvoice). */
export interface ErpInvoice extends ErpOrder {
  dueDate: string | null
}

/** GET /api/customers/{id}/erp: always 200; `available` false = the ERP is down, `message` says so. */
export interface ErpCustomerData {
  linked: boolean
  erpCustomerId: string | null
  available: boolean
  message: string | null
  orders: ErpOrder[]
  invoices: ErpInvoice[]
  fetchedAt: string | null
}

export interface ErpSyncLogEntry {
  id: string
  customerId: string
  customerName: string | null
  erpCustomerId: string
  result: 'success' | 'failed' | 'not_configured'
  error: string | null
  createdAt: string
}

export function getCustomerErp(customerId: string, signal?: AbortSignal): Promise<ErpCustomerData> {
  return apiGet<ErpCustomerData>(`/api/customers/${encodeURIComponent(customerId)}/erp`, signal)
}

/** Links the customer to an ERP customer; an empty id removes the link. 409 when another customer has it. */
export function linkCustomerToErp(customerId: string, erpCustomerId: string): Promise<{ customerId: string; erpCustomerId: string | null }> {
  return apiPut(`/api/customers/${encodeURIComponent(customerId)}/erp-link`, { erpCustomerId: erpCustomerId.trim() || null })
}

export function listErpSyncLogs(page = 1, signal?: AbortSignal): Promise<PagedResult<ErpSyncLogEntry>> {
  return apiGet<PagedResult<ErpSyncLogEntry>>(`/api/integrations/erp/sync-logs?page=${page}`, signal)
}
