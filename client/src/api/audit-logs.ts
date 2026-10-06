import { apiGet } from './client'
import type { PagedResult } from './paging'

/** Actions the server writes to the audit log (Crm.Domain.Audit.AuditActions) and the i18n key of each label: t(). */
export const auditActionKeys = {
  'login.succeeded': 'loginSucceeded',
  'login.failed': 'loginFailed',
  'user.created': 'userCreated',
  'user.updated': 'userUpdated',
  'user.deactivated': 'userDeactivated',
  'user.reactivated': 'userReactivated',
  'sla-policy.updated': 'slaPolicyUpdated',
  'customer.deleted': 'customerDeleted',
  'customer-contact.removed': 'customerContactRemoved',
} as const
export type AuditAction = keyof typeof auditActionKeys
export const auditActions = Object.keys(auditActionKeys) as AuditAction[]

/** One audit log line (server: AuditLogResponse). Old/new values are JSON text. */
export interface AuditLogEntry {
  id: number
  occurredAt: string
  userId: string | null
  userEmail: string | null
  action: AuditAction
  entityType: string
  entityId: string | null
  oldValues: string | null
  newValues: string | null
  ipAddress: string | null
}

export interface AuditLogParams {
  userId?: string
  action?: AuditAction
  /** ISO 8601 instants (UTC), inclusive. */
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

/** GET /api/audit-logs, newest first (needs audit.view). */
export function listAuditLogs(params: AuditLogParams, signal?: AbortSignal): Promise<PagedResult<AuditLogEntry>> {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.set(key, String(value))
  }
  const queryString = query.toString()
  return apiGet<PagedResult<AuditLogEntry>>(queryString ? `/api/audit-logs?${queryString}` : '/api/audit-logs', signal)
}
