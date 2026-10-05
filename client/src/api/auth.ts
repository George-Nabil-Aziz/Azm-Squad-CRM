import type { Permission } from '@/auth/permissions'
import { apiGet, apiPost } from './client'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: 'Bearer'
  /** ISO 8601 UTC. */
  expiresAt: string
}

export interface CurrentUser {
  id: string
  email: string
  fullName: string
  roles: string[]
  /** Permissions of the user's roles (server: RolePermissions), in catalogue order. */
  permissions: Permission[]
}

export function login(request: LoginRequest): Promise<LoginResponse> {
  return apiPost<LoginResponse>('/api/auth/login', request)
}

export function getCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return apiGet<CurrentUser>('/api/auth/me', signal)
}
