import { apiDelete, apiGet, apiPost, apiPut } from './client'
import { listPath, type ListParams, type PagedResult } from './paging'

/** A customer profile (server: CustomerResponse). Times are UTC ISO strings. */
export interface Customer {
  id: string
  name: string
  email: string | null
  phone: string | null
  createdAt: string
  updatedAt: string
}

/** Body of create and edit. Only the name is required; send null for an empty email or phone. */
export interface CustomerRequest {
  name: string
  email: string | null
  phone: string | null
}

/** GET /api/customers: `search` matches name, phone or email. */
export function listCustomers(params: ListParams, signal?: AbortSignal): Promise<PagedResult<Customer>> {
  return apiGet<PagedResult<Customer>>(listPath('/api/customers', params), signal)
}

export function createCustomer(request: CustomerRequest): Promise<Customer> {
  return apiPost<Customer>('/api/customers', request)
}

export function updateCustomer(id: string, request: CustomerRequest): Promise<Customer> {
  return apiPut<Customer>(`/api/customers/${encodeURIComponent(id)}`, request)
}

/** Soft delete on the server: the customer leaves every list; its tickets stay. */
export function deleteCustomer(id: string): Promise<void> {
  return apiDelete(`/api/customers/${encodeURIComponent(id)}`)
}
