import type { Permission } from '@/auth/permissions'
import { apiGet, apiGetQuiet, apiPost } from './client'

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

/** A development demo sign-in (server: DemoAccount); never carries a password. */
export interface DemoAccount {
  email: string
  /** A staff role name, or the portal demo customer (role "customer" in any letter case). */
  role: string
}

/** GET /api/auth/demo-accounts: answers only in Development (404 elsewhere); a failure is not shown to the user. */
export function getDemoAccounts(signal?: AbortSignal): Promise<DemoAccount[]> {
  return apiGetQuiet<DemoAccount[]>('/api/auth/demo-accounts', signal)
}
