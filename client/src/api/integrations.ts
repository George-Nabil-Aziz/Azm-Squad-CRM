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
