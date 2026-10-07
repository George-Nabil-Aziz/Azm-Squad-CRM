import { apiDelete, apiGet, apiPost } from './client'

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
