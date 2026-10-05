/** RFC 7807 body returned by the API for every error (see server/src/Crm.Api/ErrorHandling). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** Field name (camelCase) → messages. Present on 400 validation errors. */
  errors?: Record<string, string[]>
  correlationId?: string
  traceId?: string
}

/** Thrown by the API client for every failed call. `status` is 0 when the server could not be reached. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined
  readonly correlationId: string | undefined

  constructor(message: string, status: number, problem?: ProblemDetails, correlationId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
    this.correlationId = correlationId ?? problem?.correlationId
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}
