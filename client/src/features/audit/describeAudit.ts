import type { AuditLogEntry } from '@/api/audit-logs'

export type DiffKind = 'added' | 'removed' | 'changed'

/** One line of the field-by-field diff; null on a side means the field does not exist there. */
export interface DiffRow {
  field: string
  old: string | null
  new: string | null
  kind: DiffKind
}

export interface AuditDescription {
  /** Key under auditLogs.summary.* and its interpolation values. */
  summary: { key: string; values: Record<string, unknown> }
  diff: DiffRow[]
}

type Flat = Map<string, string>

function parse(text: string | null): unknown {
  if (text === null || text === '') return undefined
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

function scalar(value: unknown): string {
  if (value === null || value === undefined) return ''
  return typeof value === 'object' ? JSON.stringify(value) : String(value)
}

/** Nested objects become dotted paths; arrays of plain values become "a, b". */
function flatten(value: unknown, prefix = '', into: Flat = new Map()): Flat {
  if (value === undefined) return into
  if (Array.isArray(value)) {
    into.set(prefix || 'value', value.map(scalar).join(', '))
  } else if (value !== null && typeof value === 'object') {
    for (const [key, inner] of Object.entries(value)) flatten(inner, prefix ? `${prefix}.${key}` : key, into)
  } else {
    into.set(prefix || 'value', scalar(value))
  }
  return into
}

/** The settings log wraps the saved settings as { response, secretsChanged }: show the settings themselves. */
function unwrapSettings(value: unknown): unknown {
  if (value !== null && typeof value === 'object' && !Array.isArray(value) && 'response' in value) {
    const { response, ...rest } = value as Record<string, unknown>
    return { ...(response as object), ...rest }
  }
  return value
}

/** Field-by-field difference of two JSON texts; unchanged fields are left out. */
export function diffValues(oldText: string | null, newText: string | null): DiffRow[] {
  const before = flatten(unwrapSettings(parse(oldText)))
  const after = flatten(unwrapSettings(parse(newText)))
  const rows: DiffRow[] = []
  for (const [field, oldValue] of before) {
    if (!after.has(field)) rows.push({ field, old: oldValue, new: null, kind: 'removed' })
    else if (after.get(field) !== oldValue) rows.push({ field, old: oldValue, new: after.get(field)!, kind: 'changed' })
  }
  for (const [field, newValue] of after) {
    if (!before.has(field)) rows.push({ field, old: null, new: newValue, kind: 'added' })
  }
  return rows
}

function asObject(text: string | null): Record<string, unknown> {
  const value = unwrapSettings(parse(text))
  return value !== null && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {}
}

function list(value: unknown): string {
  return Array.isArray(value) ? value.map(scalar).join(', ') : scalar(value)
}

export interface DescribeContext {
  /** User id to full name, for entries whose values do not carry the name. */
  userNames?: Record<string, string>
}

/** A readable sentence (as an i18n key + values) and the clean diff for one audit entry. */
export function describeAuditEntry(entry: AuditLogEntry, context: DescribeContext = {}): AuditDescription {
  const oldObject = asObject(entry.oldValues)
  const newObject = asObject(entry.newValues)
  const diff = diffValues(entry.oldValues, entry.newValues)
  const summary = (key: string, values: Record<string, unknown> = {}) => ({ summary: { key, values }, diff })
  const userName = () =>
    scalar(newObject.fullName) ||
    scalar(oldObject.fullName) ||
    (entry.entityId ? context.userNames?.[entry.entityId] : undefined) ||
    scalar(newObject.email) ||
    scalar(oldObject.email) ||
    entry.entityId ||
    ''

  switch (entry.action) {
    case 'login.succeeded':
      return summary('signedIn')
    case 'login.failed':
      return summary('signInFailed', { reason: scalar(newObject.reason) })
    case 'user.created':
      return summary('userCreated', { name: userName(), roles: list(newObject.roles) })
    case 'user.updated': {
      if ('roles' in oldObject || 'roles' in newObject) {
        const from = list(oldObject.roles)
        const to = list(newObject.roles)
        if (from !== to) return summary('roleChanged', { name: userName(), from, to })
      }
      if ('branchId' in oldObject || 'branchId' in newObject) return summary('userBranchChanged', { name: userName() })
      return summary('userUpdated', { name: userName() })
    }
    case 'user.deactivated':
      return summary('userDeactivated', { name: userName() })
    case 'user.reactivated':
      return summary('userReactivated', { name: userName() })
    case 'sla-policy.updated': {
      const priority = (entry.entityId ?? '').split('/').pop() ?? ''
      if (entry.newValues === null) return summary('slaRemoved', { priority })
      const changes = (['response', 'resolution'] as const).flatMap((part) => {
        const from = scalar(oldObject[`${part}Minutes`])
        const to = scalar(newObject[`${part}Minutes`])
        return from !== to ? [{ part, from, to }] : []
      })
      return summary(entry.oldValues === null ? 'slaSet' : 'slaChanged', { priority, changes })
    }
    case 'customer.deleted':
      return summary('customerDeleted', { name: scalar(oldObject.name) || entry.entityId || '' })
    case 'customer-contact.removed':
      return summary('contactRemoved', { type: scalar(oldObject.type), value: scalar(oldObject.value) })
    case 'settings.updated':
      return summary('settingsChanged')
    case 'branding.updated':
      return summary('brandingChanged')
    default:
      return summary('generic', { action: entry.action })
  }
}

export type FormattedIp = { kind: 'local' } | { kind: 'unknown' } | { kind: 'address'; value: string }

/** "Local" for loopback, "unknown" when missing, otherwise the address (IPv4-mapped IPv6 shown as IPv4). */
export function formatIpAddress(ip: string | null): FormattedIp {
  const value = ip?.trim().replace(/^::ffff:/i, '') ?? ''
  if (value === '') return { kind: 'unknown' }
  if (value === '::1' || value === 'localhost' || value.startsWith('127.')) return { kind: 'local' }
  return { kind: 'address', value }
}
