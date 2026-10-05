/**
 * The only place that stores the access token (CLAUDE.md: "Auth token kept by client/src/auth/").
 * Components never read the token: they use useIsAuthenticated(); the API client calls getAccessToken().
 */
const STORAGE_KEY = 'crm.session'

interface StoredSession {
  accessToken: string
  /** ISO 8601 UTC, from the login response `expiresAt`. */
  expiresAt: string
}

type Listener = () => void

const listeners = new Set<Listener>()

function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredSession>
    return typeof parsed.accessToken === 'string' && typeof parsed.expiresAt === 'string'
      ? { accessToken: parsed.accessToken, expiresAt: parsed.expiresAt }
      : null
  } catch {
    return null
  }
}

function notify() {
  listeners.forEach((listener) => listener())
}

/** The current token, or null when signed out or when the stored token has expired. */
export function getAccessToken(): string | null {
  const session = readSession()
  if (!session) return null
  const expiresAt = Date.parse(session.expiresAt)
  if (Number.isNaN(expiresAt) || expiresAt <= Date.now()) return null
  return session.accessToken
}

export function saveSession(accessToken: string, expiresAt: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ accessToken, expiresAt } satisfies StoredSession))
  } catch {
    // Storage blocked (private mode): the user stays signed out.
  }
  notify()
}

export function clearSession(): void {
  try {
    localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Nothing stored.
  }
  notify()
}

/** Subscribe to sign-in / sign-out, including changes made in other browser tabs. */
export function subscribeToSession(listener: Listener): () => void {
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
