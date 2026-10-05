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

async function request<T>(method: string, path: string, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, { method, headers: { Accept: 'application/json' }, signal })
  } catch (error) {
    // Cancelled on purpose (component unmounted): not a failure the user must see.
    if (signal?.aborted) throw error
    return fail(new ApiError(`${method} ${path} failed: network error`, 0))
  }

  if (!response.ok) {
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

  return (await response.json()) as T
}

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, signal)
}
