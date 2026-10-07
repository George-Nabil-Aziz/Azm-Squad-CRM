import type { TicketPriority } from '@/features/tickets/ticket-values'
import { apiDelete, apiGet, apiPost, apiPut } from './client'

/** A department (server: DepartmentResponse). Inactive departments stay on old tickets only. */
export interface Department {
  id: string
  name: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

/** Body of create and edit. Names are unique ignoring case and spaces (the server answers 400 otherwise). */
export interface DepartmentRequest {
  name: string
  isActive: boolean
}

/** An SLA override of one department and priority (server: DepartmentSlaPolicyResponse). */
export interface DepartmentSlaPolicy {
  departmentId: string
  priority: TicketPriority
  responseMinutes: number
  resolutionMinutes: number
  updatedAt: string
}

export interface DepartmentListParams {
  /** Only the departments tickets may be put in. */
  activeOnly?: boolean
}

/** GET /api/departments: every department ordered by name (not paged: a short list). */
export function listDepartments({ activeOnly }: DepartmentListParams, signal?: AbortSignal): Promise<Department[]> {
  return apiGet<Department[]>(activeOnly ? '/api/departments?activeOnly=true' : '/api/departments', signal)
}

export function createDepartment(request: DepartmentRequest): Promise<Department> {
  return apiPost<Department>('/api/departments', request)
}

export function updateDepartment(id: string, request: DepartmentRequest): Promise<Department> {
  return apiPut<Department>(`/api/departments/${encodeURIComponent(id)}`, request)
}

/** The SLA overrides of a department (sla.manage). */
export function listDepartmentSlaPolicies(id: string, signal?: AbortSignal): Promise<DepartmentSlaPolicy[]> {
  return apiGet<DepartmentSlaPolicy[]>(`/api/departments/${encodeURIComponent(id)}/sla-policies`, signal)
}

export function setDepartmentSlaPolicy(
  id: string,
  priority: TicketPriority,
  request: { responseMinutes: number; resolutionMinutes: number },
): Promise<DepartmentSlaPolicy> {
  return apiPut<DepartmentSlaPolicy>(
    `/api/departments/${encodeURIComponent(id)}/sla-policies/${encodeURIComponent(priority)}`,
    request,
  )
}

/** Removes the override: the global policy of the priority applies again. */
export function removeDepartmentSlaPolicy(id: string, priority: TicketPriority): Promise<void> {
  return apiDelete(`/api/departments/${encodeURIComponent(id)}/sla-policies/${encodeURIComponent(priority)}`)
}

/** The department of a ticket after a transfer (the caller may no longer see the ticket). */
export interface TicketDepartment {
  ticketId: string
  departmentId: string | null
  departmentName: string | null
}

/** Moves a ticket to another department (null = general); the server records it in the ticket history. */
export function transferTicketDepartment(ticketId: string, departmentId: string | null): Promise<TicketDepartment> {
  return apiPut<TicketDepartment>(`/api/tickets/${encodeURIComponent(ticketId)}/department`, { departmentId })
}
