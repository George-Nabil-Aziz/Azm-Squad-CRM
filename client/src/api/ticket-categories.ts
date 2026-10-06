import { apiGet, apiPost, apiPut } from './client'

/** A ticket category (server: TicketCategoryResponse). Inactive categories stay on old tickets only. */
export interface TicketCategory {
  id: string
  name: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

/** Body of create and edit. Names are unique ignoring case and spaces (the server answers 400 otherwise). */
export interface TicketCategoryRequest {
  name: string
  isActive: boolean
}

export interface TicketCategoryListParams {
  /** Only the categories new tickets may use. */
  activeOnly?: boolean
}

/** GET /api/ticket-categories: every category ordered by name (not paged: a short list). */
export function listTicketCategories(
  { activeOnly }: TicketCategoryListParams,
  signal?: AbortSignal,
): Promise<TicketCategory[]> {
  const path = activeOnly ? '/api/ticket-categories?activeOnly=true' : '/api/ticket-categories'
  return apiGet<TicketCategory[]>(path, signal)
}

export function createTicketCategory(request: TicketCategoryRequest): Promise<TicketCategory> {
  return apiPost<TicketCategory>('/api/ticket-categories', request)
}

export function updateTicketCategory(id: string, request: TicketCategoryRequest): Promise<TicketCategory> {
  return apiPut<TicketCategory>(`/api/ticket-categories/${encodeURIComponent(id)}`, request)
}
