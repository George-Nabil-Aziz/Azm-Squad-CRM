import { apiGet, apiPut } from './client'

/** An agent on the assignment settings (server: AssignmentAgentResponse). */
export interface AssignmentAgent {
  id: string
  fullName: string
  /** On duty = may receive automatic assignments. */
  onDuty: boolean
  /** Tickets assigned to the agent that are not resolved or closed. */
  openTickets: number
}

/** GET /api/settings/assignment (needs tickets.assign). */
export interface AssignmentSettings {
  autoAssignEnabled: boolean
  agents: AssignmentAgent[]
}

export function getAssignmentSettings(signal?: AbortSignal): Promise<AssignmentSettings> {
  return apiGet<AssignmentSettings>('/api/settings/assignment', signal)
}

export function setAutoAssign(autoAssignEnabled: boolean): Promise<AssignmentSettings> {
  return apiPut<AssignmentSettings>('/api/settings/assignment', { autoAssignEnabled })
}

export function setAgentOnDuty(userId: string, onDuty: boolean): Promise<AssignmentSettings> {
  return apiPut<AssignmentSettings>(`/api/settings/assignment/agents/${encodeURIComponent(userId)}`, { onDuty })
}
