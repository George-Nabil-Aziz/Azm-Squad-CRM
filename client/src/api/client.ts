import { clearPortalSession, getPortalAccessToken } from '../auth/portal-session'
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
  /** JSON body; a FormData body is sent as multipart/form-data (the browser sets the boundary). */
  body?: unknown
  signal?: AbortSignal
  /** 'blob' reads a file download instead of JSON. */
  responseType?: 'json' | 'blob'
}

async function request<T>(
  method: string,
  path: string,
  { body, signal, responseType = 'json' }: RequestOptions = {},
): Promise<T> {
  const isForm = body instanceof FormData
  // Accept-Language: the API answers validation messages and ProblemDetails in the UI language (ar / en).
  const headers: Record<string, string> = { Accept: 'application/json', 'Accept-Language': getLanguage() }
  if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  // Portal calls carry the customer's token, everything else the staff token (a customer is not a staff user).
  const isPortal = path.startsWith('/api/portal/')
  const accessToken = isPortal ? getPortalAccessToken() : getAccessToken()
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  let response: Response
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // Cancelled on purpose (component unmounted): not a failure the user must see.
    if (signal?.aborted) throw error
    return fail(new ApiError(`${method} ${path} failed: network error`, 0))
  }

  if (!response.ok) {
    // Token expired or revoked: sign out, so the app shows the sign-in form again.
    if (response.status === 401 && accessToken) {
      if (isPortal) clearPortalSession()
      else clearSession()
    }
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
  if (responseType === 'blob') return (await response.blob()) as T
  return (await response.json()) as T
}

/** POST of a multipart form (file uploads). */
export function apiPostForm<T>(path: string, form: FormData, signal?: AbortSignal): Promise<T> {
  return request<T>('POST', path, { body: form, signal })
}

/** GET of a file: the response body as a Blob (sent with the access token, unlike a plain link). */
export function apiGetBlob(path: string, signal?: AbortSignal): Promise<Blob> {
  return request<Blob>('GET', path, { signal, responseType: 'blob' })
}

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, { signal })
}

export function apiPost<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>('POST', path, { body, signal })
}

export function apiPut<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>('PUT', path, { body, signal })
}

export function apiDelete<T = void>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('DELETE', path, { signal })
}
