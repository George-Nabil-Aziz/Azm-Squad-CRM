/**
 * The session of a signed-in portal customer (CRM-40), kept apart from the staff session in ./session.ts: a customer is
 * not a staff user, so the two tokens never mix. Components never read the token: they use usePortalSession(); the API
 * client calls getPortalAccessToken() for /api/portal/ requests.
 */
const STORAGE_KEY = 'crm.portal-session'

export interface PortalCustomer {
  id: string
  name: string
  email: string
}

interface StoredPortalSession {
  accessToken: string
  /** ISO 8601 UTC, from the sign-in response `expiresAt`. */
  expiresAt: string
  customer: PortalCustomer
}

type Listener = () => void

const listeners = new Set<Listener>()

function isCustomer(value: unknown): value is PortalCustomer {
  const customer = value as Partial<PortalCustomer> | null
  return (
    typeof customer?.id === 'string' && typeof customer.name === 'string' && typeof customer.email === 'string'
  )
}

function readSession(): StoredPortalSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredPortalSession>
    if (typeof parsed.accessToken !== 'string' || typeof parsed.expiresAt !== 'string' || !isCustomer(parsed.customer)) {
      return null
    }
    const expiresAt = Date.parse(parsed.expiresAt)
    if (Number.isNaN(expiresAt) || expiresAt <= Date.now()) return null
    return { accessToken: parsed.accessToken, expiresAt: parsed.expiresAt, customer: parsed.customer }
  } catch {
    return null
  }
}

/** The portal token, or null when signed out or when the stored token has expired. */
export function getPortalAccessToken(): string | null {
  return readSession()?.accessToken ?? null
}

/** The signed-in customer, or null. */
export function getPortalCustomer(): PortalCustomer | null {
  return readSession()?.customer ?? null
}

export function savePortalSession(accessToken: string, expiresAt: string, customer: PortalCustomer): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ accessToken, expiresAt, customer } satisfies StoredPortalSession))
  } catch {
    // Storage blocked (private mode): the customer stays signed out.
  }
  listeners.forEach((listener) => listener())
}

export function clearPortalSession(): void {
  try {
    localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Nothing stored.
  }
  listeners.forEach((listener) => listener())
}

/** Subscribe to portal sign-in / sign-out, including changes made in other browser tabs. */
export function subscribeToPortalSession(listener: Listener): () => void {
  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === STORAGE_KEY) listener()
  }
  listeners.add(listener)
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}
