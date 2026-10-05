import { apiGet, apiPost, apiPut } from './client'

/** Role names seeded by the API (server: Crm.Application.Auth.Roles). Labels: t(`users.roleNames.${role}`). */
export const roleNames = ['SuperAdmin', 'Admin', 'Supervisor', 'Agent'] as const
export type RoleName = (typeof roleNames)[number]

export interface User {
  id: string
  email: string
  fullName: string
  roles: RoleName[]
  isActive: boolean
}

/** One page of a list (server: PagedResult<T>). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
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
}

export interface UpdateUserRequest {
  email: string
  fullName: string
  roles: RoleName[]
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
