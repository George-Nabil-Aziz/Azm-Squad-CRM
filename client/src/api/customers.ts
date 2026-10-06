import { apiDelete, apiGet, apiPost, apiPut } from './client'
import { listPath, type ListParams, type PagedResult } from './paging'

/** Kind of a customer contact (server: CustomerContactResponse.type). */
export type ContactType = 'phone' | 'email' | 'whatsapp'

/** One phone number, email address or WhatsApp number. Numbers are E.164 ("+966501234567"), emails lower case. */
export interface CustomerContact {
  id: string
  type: ContactType
  value: string
  isPrimary: boolean
}

/**
 * A customer profile (server: CustomerResponse). `email` / `phone` are the primary email / phone; `contacts` lists
 * every contact (phones, then emails, then WhatsApp numbers; primary first). Times are UTC ISO strings.
 */
export interface Customer {
  id: string
  name: string
  email: string | null
  phone: string | null
  createdAt: string
  updatedAt: string
  contacts: CustomerContact[]
}

/** Body of create and edit. Only the name is required; send null for an empty email or phone. */
export interface CustomerRequest {
  name: string
  email: string | null
  phone: string | null
}

/** Body of "add contact". Phone numbers may be typed in any common format; the server stores E.164. */
export interface CustomerContactRequest {
  type: ContactType
  value: string
  isPrimary: boolean
}

/** GET /api/customers: `search` matches name, phone or email. */
export function listCustomers(params: ListParams, signal?: AbortSignal): Promise<PagedResult<Customer>> {
  return apiGet<PagedResult<Customer>>(listPath('/api/customers', params), signal)
}

/** One customer with all its contacts. */
export function getCustomer(id: string, signal?: AbortSignal): Promise<Customer> {
  return apiGet<Customer>(`/api/customers/${encodeURIComponent(id)}`, signal)
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

/** Adds a contact; the first contact of a type (or one sent with isPrimary) becomes the primary one. */
export function addCustomerContact(customerId: string, request: CustomerContactRequest): Promise<CustomerContact> {
  return apiPost<CustomerContact>(`/api/customers/${encodeURIComponent(customerId)}/contacts`, request)
}

/** Makes the contact the primary one of its type (the old primary is unset). */
export function makeCustomerContactPrimary(customerId: string, contactId: string): Promise<void> {
  return apiPost<void>(
    `/api/customers/${encodeURIComponent(customerId)}/contacts/${encodeURIComponent(contactId)}/primary`,
    undefined,
  )
}

/** Removes the contact; when it was primary, the next contact of its type becomes primary. */
export function removeCustomerContact(customerId: string, contactId: string): Promise<void> {
  return apiDelete(`/api/customers/${encodeURIComponent(customerId)}/contacts/${encodeURIComponent(contactId)}`)
}
