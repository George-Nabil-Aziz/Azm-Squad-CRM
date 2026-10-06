import type { TicketChannel, TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'
import type { Customer } from './customers'
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
  /** When the ticket became resolved (UTC ISO); null while it is not (cleared on reopen). */
  resolvedAt: string | null
  /** Statuses the ticket may move to now (workflow order); empty when none. */
  allowedStatuses: TicketStatus[]
  responseBreached: boolean
  resolutionBreached: boolean
  escalationLevel: number
  responseWarnedAt: string | null
}

/** One entry of a ticket thread (server: TicketMessageResponse). "internal" notes are never shown to the customer. */
export interface TicketMessage {
  id: string
  direction: 'inbound' | 'outbound' | 'internal'
  isInternal: boolean
  body: string
  channel: TicketChannel
  authorId: string | null
  /** Null for customer messages. */
  authorName: string | null
  createdAt: string
  deliveryStatus: 'pending' | 'sent' | 'failed' | null
}

/** Body of "add a message": a reply to the customer, or (internal) a note for the team. */
export interface AddTicketMessageRequest {
  body: string
  internal: boolean
  /** Approved WhatsApp template sent instead of free text (needed more than 24 hours after the last customer message). */
  templateName?: string
}

/** Moves the ticket along the workflow; 400 on `status` for a move the workflow does not allow (see `allowedStatuses`). */
export function changeTicketStatus(ticketId: string, status: TicketStatus): Promise<Ticket> {
  return apiPut<Ticket>(`/api/tickets/${encodeURIComponent(ticketId)}/status`, { status })
}

/**
 * Gives the ticket to a staff user, or unassigns it (null). Anyone with tickets.manage may take a ticket for themselves;
 * assigning to someone else needs tickets.assign (403), an inactive user is a 400 on `assigneeId`.
 */
export function assignTicket(ticketId: string, assigneeId: string | null): Promise<Ticket> {
  return apiPost<Ticket>(`/api/tickets/${encodeURIComponent(ticketId)}/assign`, { assigneeId })
}

/** The conversation of a ticket, oldest first (internal notes included for staff). */
export function listTicketMessages(ticketId: string, signal?: AbortSignal): Promise<TicketMessage[]> {
  return apiGet<TicketMessage[]>(`/api/tickets/${encodeURIComponent(ticketId)}/messages`, signal)
}

/** Replies to the customer or adds an internal note (400 when the ticket is closed). */
export function addTicketMessage(ticketId: string, request: AddTicketMessageRequest): Promise<TicketMessage> {
  return apiPost<TicketMessage>(`/api/tickets/${encodeURIComponent(ticketId)}/messages`, request)
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

/**
 * One entry of a ticket history (server: TicketHistoryItemResponse), oldest first. `field` "escalation" is an SLA
 * escalation: `newValue` is the level and nobody changed it. Status / priority values are API codes, assignee / category
 * values names; null = none.
 */
export interface TicketHistoryItem {
  id: string
  field: 'status' | 'assignee' | 'priority' | 'category' | 'escalation'
  oldValue: string | null
  newValue: string | null
  changedById: string | null
  /** Null for system changes. */
  changedByName: string | null
  changedAt: string
}

/** The audit trail of a ticket, oldest first. Read-only: the API has no way to change or delete entries. */
export function getTicketHistory(ticketId: string, signal?: AbortSignal): Promise<TicketHistoryItem[]> {
  return apiGet<TicketHistoryItem[]>(`/api/tickets/${encodeURIComponent(ticketId)}/history`, signal)
}

/** Changes the category (null = none; 400 on `categoryId` for an inactive category). */
export function changeTicketCategory(ticketId: string, categoryId: string | null): Promise<Ticket> {
  return apiPut<Ticket>(`/api/tickets/${encodeURIComponent(ticketId)}/category`, { categoryId })
}

/** Counters of the agent dashboard (server: MyTicketCounters). `breachedToday` counts SLA breaches of the current UTC day. */
export interface MyTicketCounters {
  open: number
  pending: number
  breachedToday: number
}

/** GET /api/tickets/mine: my tickets that are not closed, nearest SLA due first, with the counters. */
export interface MyTickets {
  counters: MyTicketCounters
  tickets: PagedResult<Ticket>
}

export function getMyTickets(signal?: AbortSignal): Promise<MyTickets> {
  return apiGet<MyTickets>('/api/tickets/mine?pageSize=50', signal)
}

/** One of the customer's tickets in the customer panel (server: CustomerTicketSummary). */
export interface CustomerTicketSummary {
  id: string
  number: string
  subject: string
  status: TicketStatus
  priority: TicketPriority
  createdAt: string
  /** True for the ticket the panel is shown on. */
  isCurrent: boolean
}

/** GET /api/tickets/{id}/customer-context: the ticket's current customer, their total ticket count and the last 5 tickets. */
export interface TicketCustomerContext {
  customer: Customer
  customerDeleted: boolean
  totalTickets: number
  recentTickets: CustomerTicketSummary[]
}

export function getTicketCustomerContext(id: string, signal?: AbortSignal): Promise<TicketCustomerContext> {
  return apiGet<TicketCustomerContext>(`/api/tickets/${encodeURIComponent(id)}/customer-context`, signal)
}
