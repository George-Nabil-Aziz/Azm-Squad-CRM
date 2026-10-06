import { apiGet, apiPost, apiPostForm } from './client'

/** The signed-in portal customer (server: PortalCustomerResponse). */
export interface PortalCustomerResponse {
  id: string
  name: string
  email: string
}

/** Result of a successful portal sign-in (server: PortalLoginResponse). */
export interface PortalLoginResponse {
  accessToken: string
  tokenType: 'Bearer'
  /** ISO 8601 UTC. */
  expiresAt: string
  customer: PortalCustomerResponse
}

/** Emails a one-time code (valid 10 minutes). Answers 204 for every valid email, known or not; 400 for an invalid one. */
export function requestPortalCode(email: string): Promise<void> {
  return apiPost<void>('/api/portal/auth/request-code', { email })
}

/** Signs in with the emailed code; 401 for a wrong, expired or used code. */
export function verifyPortalCode(email: string, code: string): Promise<PortalLoginResponse> {
  return apiPost<PortalLoginResponse>('/api/portal/auth/verify', { email, code })
}

/** An active ticket category the customer can choose (server: PortalCategoryResponse); `name` is in the UI language. */
export interface PortalCategory {
  id: string
  name: string
}

/** The ticket a customer just opened (server: PortalTicketResponse); `number` is "TKT-000001". */
export interface PortalTicket {
  id: string
  number: string
  subject: string
  status: string
  createdAt: string
  attachments: { id: string; fileName: string; contentType: string; size: number }[]
}

export interface SubmitPortalTicketInput {
  subject: string
  description: string
  /** Empty = no category. */
  categoryId: string
  files: File[]
}

export function listPortalCategories(signal?: AbortSignal): Promise<PortalCategory[]> {
  return apiGet<PortalCategory[]>('/api/portal/ticket-categories', signal)
}

/** Opens a ticket (multipart); 400 on `subject`, `categoryId` or `files` (wrong type / size / too many). */
export function submitPortalTicket(input: SubmitPortalTicketInput): Promise<PortalTicket> {
  const form = new FormData()
  form.append('subject', input.subject)
  form.append('description', input.description)
  if (input.categoryId) form.append('categoryId', input.categoryId)
  for (const file of input.files) form.append('files', file)
  return apiPostForm<PortalTicket>('/api/portal/tickets', form)
}
