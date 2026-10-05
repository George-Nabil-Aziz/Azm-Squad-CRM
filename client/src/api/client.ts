import { clearSession, getAccessToken } from '../auth/session'
import { getLanguage } from '../i18n/i18n'
import { ApiError, type ProblemDetails } from './errors'

type ApiErrorListener = (error: ApiError) => void

const errorListeners = new Set<ApiErrorListener>()

/**
 * Subscribe to every failed API call (used by ApiErrorToaster to show a toast).
 * Returns an unsubscribe function.
 */
export function onApiError(listener: ApiErrorListener): () => void {
  errorListeners.add(listener)
  return () => {
    errorListeners.delete(listener)
  }
}

function fail(error: ApiError): never {
  errorListeners.forEach((listener) => listener(error))
  throw error
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get('Content-Type') ?? ''
  if (!contentType.includes('json')) return undefined
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return undefined
  }
}

interface RequestOptions {
  body?: unknown
  signal?: AbortSignal
}

async function request<T>(method: string, path: string, { body, signal }: RequestOptions = {}): Promise<T> {
  // Accept-Language: the API answers validation messages and ProblemDetails in the UI language (ar / en).
  const headers: Record<string, string> = { Accept: 'application/json', 'Accept-Language': getLanguage() }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const accessToken = getAccessToken()
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  let response: Response
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // Cancelled on purpose (component unmounted): not a failure the user must see.
    if (signal?.aborted) throw error
    return fail(new ApiError(`${method} ${path} failed: network error`, 0))
  }

  if (!response.ok) {
    // Token expired or revoked: sign out, so the app shows the sign-in form again.
    if (response.status === 401 && accessToken) clearSession()
    const problem = await readProblem(response)
    return fail(
      new ApiError(
        `${method} ${path} failed with status ${response.status}`,
        response.status,
        problem,
        response.headers.get('X-Correlation-Id') ?? undefined,
      ),
    )
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, { signal })
}

export function apiPost<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>('POST', path, { body, signal })
}
