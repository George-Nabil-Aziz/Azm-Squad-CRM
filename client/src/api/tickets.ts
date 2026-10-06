import type { TicketChannel, TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'
import { apiGet, apiPost, apiPut } from './client'
import type { PagedResult } from './paging'

/**
 * GET /api/tickets query (every given filter must match). Dates are "yyyy-MM-dd" (inclusive, UTC days); `search` is a
 * ticket number ("TKT-000012", "12") or text in the subject; `unassigned` and `assigneeId` exclude each other.
 */
export interface TicketListParams {
  status?: TicketStatus
  priority?: TicketPriority
  categoryId?: string
  assigneeId?: string
  unassigned?: boolean
  createdFrom?: string
  createdTo?: string
  search?: string
  page?: number
  pageSize?: number
}

/** A staff user tickets can be assigned to (server: TicketAssigneeResponse). */
export interface TicketAssignee {
  id: string
  fullName: string
}

/** Query-string order of the list parameters. */
const listParamOrder = [
  'status',
  'priority',
  'categoryId',
  'assigneeId',
  'unassigned',
  'createdFrom',
  'createdTo',
  'search',
  'page',
  'pageSize',
] as const satisfies readonly (keyof TicketListParams)[]

/** One page of tickets, newest first. */
export function listTickets(params: TicketListParams, signal?: AbortSignal): Promise<PagedResult<Ticket>> {
  const query = new URLSearchParams()
  for (const key of listParamOrder) {
    const value = params[key]
    if (value !== undefined && value !== '' && value !== false) query.set(key, String(value))
  }
  const queryString = query.toString()
  return apiGet<PagedResult<Ticket>>(queryString ? `/api/tickets?${queryString}` : '/api/tickets', signal)
}

/** Active staff users tickets can be assigned to, ordered by name. */
export function listTicketAssignees(signal?: AbortSignal): Promise<TicketAssignee[]> {
  return apiGet<TicketAssignee[]>('/api/tickets/assignees', signal)
}

/**
 * A ticket (server: TicketResponse). `number` is "TKT-000001"; names of a deleted customer or a deactivated category
 * are still shown. Times are UTC ISO strings.
 */
export interface Ticket {
  id: string
  number: string
  subject: string
  description: string | null
  status: TicketStatus
  priority: TicketPriority
  channel: TicketChannel
  customerId: string
  customerName: string
  categoryId: string | null
  categoryName: string | null
  assigneeId: string | null
  assigneeName: string | null
  createdAt: string
  updatedAt: string
  responseDueAt: string | null
  resolutionDueAt: string | null
  firstResponseAt: string | null
  resolvedAt: string | null
}

/** Body of "create ticket". Customer and subject are required; send null for no description / category. */
export interface CreateTicketRequest {
  customerId: string
  subject: string
  description: string | null
  categoryId: string | null
  priority: TicketPriority
}

/** Creates a ticket in status "new"; the server gives it the next ticket number. */
export function createTicket(request: CreateTicketRequest): Promise<Ticket> {
  return apiPost<Ticket>('/api/tickets', request)
}

export function getTicket(id: string, signal?: AbortSignal): Promise<Ticket> {
  return apiGet<Ticket>(`/api/tickets/${encodeURIComponent(id)}`, signal)
}

/** Changes the priority; the server recalculates the SLA due times from the creation time (CRM-20). */
export function changeTicketPriority(id: string, priority: TicketPriority): Promise<Ticket> {
  return apiPut<Ticket>(`/api/tickets/${encodeURIComponent(id)}/priority`, { priority })
}
