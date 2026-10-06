import { apiPost } from './client'

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
