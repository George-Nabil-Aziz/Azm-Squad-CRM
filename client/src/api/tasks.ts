import { apiGet, apiPost } from './client'

/** A task of the signed-in user (server: TaskResponse). Times are UTC ISO 8601. */
export interface WorkTask {
  id: string
  title: string
  description: string | null
  dueAt: string
  ticketId: string | null
  /** "TKT-000001" when linked to a ticket. */
  ticketNumber: string | null
  isDone: boolean
  completedAt: string | null
  createdAt: string
}

/** Body of POST /api/tasks: `dueAt` must be in the future (the server answers 400 otherwise). */
export interface CreateTaskRequest {
  title: string
  description?: string | null
  dueAt: string
  ticketId?: string | null
}

/** GET /api/tasks: my open tasks by due time, or the done ones. */
export function listTasks(status: 'open' | 'done' = 'open', signal?: AbortSignal): Promise<WorkTask[]> {
  return apiGet<WorkTask[]>(`/api/tasks?status=${status}`, signal)
}

export function createTask(request: CreateTaskRequest): Promise<WorkTask> {
  return apiPost<WorkTask>('/api/tasks', request)
}

export function markTaskDone(id: string): Promise<WorkTask> {
  return apiPost<WorkTask>(`/api/tasks/${encodeURIComponent(id)}/done`, {})
}
