import { apiDelete, apiGet, apiGetBlob, apiPost, apiPostForm, apiPut } from './client'
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

/** Category of a timeline entry (server: InteractionTypes). */
export type InteractionType = 'customer' | 'note' | 'attachment' | 'ticket' | 'message'

/**
 * One entry of a customer's timeline (server: CustomerInteractionResponse). `event` is a code such as
 * "customerCreated" (translated under customers.timeline.events); `actorName` is null for system / channel entries.
 */
export interface CustomerInteraction {
  id: number
  type: InteractionType
  event: string
  details: string | null
  sourceId: string | null
  actorId: string | null
  actorName: string | null
  occurredAt: string
}

/** Query of GET /api/customers/{id}/timeline: optional type filter and paging. */
export interface TimelineParams {
  type?: InteractionType
  page?: number
  pageSize?: number
}

/** The customer's interaction history, newest first. */
export function getCustomerTimeline(
  id: string,
  { type, page, pageSize }: TimelineParams,
  signal?: AbortSignal,
): Promise<PagedResult<CustomerInteraction>> {
  const query = new URLSearchParams()
  if (type) query.set('type', type)
  if (page !== undefined) query.set('page', String(page))
  if (pageSize !== undefined) query.set('pageSize', String(pageSize))
  const queryString = query.toString()
  const path = `/api/customers/${encodeURIComponent(id)}/timeline`
  return apiGet<PagedResult<CustomerInteraction>>(queryString ? `${path}?${queryString}` : path, signal)
}

/** A note about a customer (server: CustomerNoteResponse). `authorName` is null when the user no longer exists. */
export interface CustomerNote {
  id: string
  text: string
  authorId: string | null
  authorName: string | null
  createdAt: string
}

/** A file attached to a customer (server: CustomerAttachmentResponse). `size` in bytes. */
export interface CustomerAttachment {
  id: string
  fileName: string
  contentType: string
  size: number
  uploadedById: string | null
  uploadedByName: string | null
  uploadedAt: string
}

/** The customer's notes, newest first. */
export function listCustomerNotes(
  id: string,
  params: Omit<ListParams, 'search'>,
  signal?: AbortSignal,
): Promise<PagedResult<CustomerNote>> {
  return apiGet<PagedResult<CustomerNote>>(listPath(`/api/customers/${encodeURIComponent(id)}/notes`, params), signal)
}

/** Adds a note written by the signed-in user. */
export function addCustomerNote(id: string, text: string): Promise<CustomerNote> {
  return apiPost<CustomerNote>(`/api/customers/${encodeURIComponent(id)}/notes`, { text })
}

/** Every file of the customer, newest first. */
export function listCustomerAttachments(id: string, signal?: AbortSignal): Promise<CustomerAttachment[]> {
  return apiGet<CustomerAttachment[]>(`/api/customers/${encodeURIComponent(id)}/attachments`, signal)
}

/** Uploads a file (multipart field "file"); the server allows certain types up to 10 MB. */
export function uploadCustomerAttachment(id: string, file: File): Promise<CustomerAttachment> {
  const form = new FormData()
  form.append('file', file)
  return apiPostForm<CustomerAttachment>(`/api/customers/${encodeURIComponent(id)}/attachments`, form)
}

/** The file's content, read through the authorized API. */
export function downloadCustomerAttachment(id: string, attachmentId: string): Promise<Blob> {
  return apiGetBlob(`/api/customers/${encodeURIComponent(id)}/attachments/${encodeURIComponent(attachmentId)}`)
}

/** Removes the contact; when it was primary, the next contact of its type becomes primary. */
export function removeCustomerContact(customerId: string, contactId: string): Promise<void> {
  return apiDelete(`/api/customers/${encodeURIComponent(customerId)}/contacts/${encodeURIComponent(contactId)}`)
}
