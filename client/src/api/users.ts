import { apiGet, apiPost, apiPut } from './client'
import type { PagedResult } from './paging'

// PagedResult moved to ./paging (shared by every list); re-exported for the existing imports.
export type { PagedResult }

/** Role names seeded by the API (server: Crm.Application.Auth.Roles). Labels: t(`users.roleNames.${role}`). */
export const roleNames = ['SuperAdmin', 'Admin', 'Supervisor', 'Agent'] as const
export type RoleName = (typeof roleNames)[number]

export interface User {
  id: string
  email: string
  fullName: string
  roles: RoleName[]
  isActive: boolean
  /** Departments the user belongs to (CRM-61). */
  departmentIds?: string[]
}

export interface ListUsersParams {
  search?: string
  page?: number
  pageSize?: number
}

export interface CreateUserRequest {
  email: string
  fullName: string
  password: string
  roles: RoleName[]
  departmentIds?: string[]
}

export interface UpdateUserRequest {
  email: string
  fullName: string
  roles: RoleName[]
  /** Omit to keep the user's departments; an empty list removes them all. */
  departmentIds?: string[]
}

export function listUsers({ search, page, pageSize }: ListUsersParams, signal?: AbortSignal): Promise<PagedResult<User>> {
  const query = new URLSearchParams()
  if (search) query.set('search', search)
  if (page !== undefined) query.set('page', String(page))
  if (pageSize !== undefined) query.set('pageSize', String(pageSize))
  const queryString = query.toString()
  return apiGet<PagedResult<User>>(queryString ? `/api/users?${queryString}` : '/api/users', signal)
}

export function createUser(request: CreateUserRequest): Promise<User> {
  return apiPost<User>('/api/users', request)
}

export function updateUser(id: string, request: UpdateUserRequest): Promise<User> {
  return apiPut<User>(`/api/users/${encodeURIComponent(id)}`, request)
}

export function deactivateUser(id: string): Promise<void> {
  return apiPost<void>(`/api/users/${encodeURIComponent(id)}/deactivate`, undefined)
}

export function reactivateUser(id: string): Promise<void> {
  return apiPost<void>(`/api/users/${encodeURIComponent(id)}/reactivate`, undefined)
}
