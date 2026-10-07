import { describe, expect, it } from 'vitest'
import type { AuditLogEntry } from '@/api/audit-logs'
import { describeAuditEntry, diffValues, formatIpAddress } from './describeAudit'

function entry(partial: Partial<AuditLogEntry>): AuditLogEntry {
  return {
    id: 1,
    occurredAt: '2026-10-06T09:00:00Z',
    userId: 'u-1',
    userEmail: 'admin@crm.local',
    action: 'user.updated',
    entityType: 'User',
    entityId: 'u-2',
    oldValues: null,
    newValues: null,
    ipAddress: null,
    ...partial,
  }
}

describe('diffValues', () => {
  it('lists changed keys only, as old and new', () => {
    const rows = diffValues('{"a":1,"b":"same"}', '{"a":2,"b":"same"}')
    expect(rows).toEqual([{ field: 'a', old: '1', new: '2', kind: 'changed' }])
  })

  it('marks keys that exist on one side only as added or removed', () => {
    const rows = diffValues('{"gone":"x"}', '{"fresh":"y"}')
    expect(rows).toContainEqual({ field: 'gone', old: 'x', new: null, kind: 'removed' })
    expect(rows).toContainEqual({ field: 'fresh', old: null, new: 'y', kind: 'added' })
  })

  it('treats only-new values as all added and only-old values as all removed', () => {
    expect(diffValues(null, '{"a":1}')).toEqual([{ field: 'a', old: null, new: '1', kind: 'added' }])
    expect(diffValues('{"a":1}', null)).toEqual([{ field: 'a', old: '1', new: null, kind: 'removed' }])
  })

  it('flattens nested objects with dotted paths and joins arrays', () => {
    const rows = diffValues('{"hours":{"start":"08:00"},"roles":["Agent"]}', '{"hours":{"start":"09:00"},"roles":["Agent","Admin"]}')
    expect(rows).toEqual([
      { field: 'hours.start', old: '08:00', new: '09:00', kind: 'changed' },
      { field: 'roles', old: 'Agent', new: 'Agent, Admin', kind: 'changed' },
    ])
  })

  it('shows text that is not JSON as one value and nothing for empty input', () => {
    expect(diffValues(null, 'plain text')).toEqual([{ field: 'value', old: null, new: 'plain text', kind: 'added' }])
    expect(diffValues(null, null)).toEqual([])
  })
})

describe('describeAuditEntry', () => {
  it('describes a role change with the user name and both roles', () => {
    const d = describeAuditEntry(
      entry({
        oldValues: '{"email":"sara@crm.local","fullName":"Sara Ahmed","roles":["Agent"]}',
        newValues: '{"email":"sara@crm.local","fullName":"Sara Ahmed","roles":["Supervisor"]}',
      }),
    )
    expect(d.summary).toEqual({ key: 'roleChanged', values: { name: 'Sara Ahmed', from: 'Agent', to: 'Supervisor' } })
    expect(d.diff).toEqual([{ field: 'roles', old: 'Agent', new: 'Supervisor', kind: 'changed' }])
  })

  it('describes sign-in and failed sign-in', () => {
    expect(describeAuditEntry(entry({ action: 'login.succeeded' })).summary.key).toBe('signedIn')
    const failed = describeAuditEntry(
      entry({ action: 'login.failed', userId: null, userEmail: 'x@y.z', newValues: '{"reason":"wrong-password"}' }),
    )
    expect(failed.summary).toEqual({ key: 'signInFailed', values: { reason: 'wrong-password' } })
  })

  it('describes an SLA policy change with the priority and the changed minutes', () => {
    const d = describeAuditEntry(
      entry({
        action: 'sla-policy.updated',
        entityType: 'SlaPolicy',
        entityId: 'High',
        oldValues: '{"responseMinutes":60,"resolutionMinutes":480}',
        newValues: '{"responseMinutes":30,"resolutionMinutes":480}',
      }),
    )
    expect(d.summary).toEqual({ key: 'slaChanged', values: { priority: 'High', changes: [{ part: 'response', from: '60', to: '30' }] } })
  })

  it('describes a department SLA policy by its priority (the last part of the entity id)', () => {
    const d = describeAuditEntry(
      entry({
        action: 'sla-policy.updated',
        entityType: 'DepartmentSlaPolicy',
        entityId: 'dep-1/Low',
        oldValues: '{"responseMinutes":60,"resolutionMinutes":480}',
        newValues: null,
      }),
    )
    expect(d.summary).toEqual({ key: 'slaRemoved', values: { priority: 'Low' } })
  })

  it('describes a customer deletion with the name', () => {
    const d = describeAuditEntry(entry({ action: 'customer.deleted', entityType: 'Customer', oldValues: '{"name":"Acme","email":"a@acme.com"}' }))
    expect(d.summary).toEqual({ key: 'customerDeleted', values: { name: 'Acme' } })
  })

  it('uses the user name lookup when the values carry no name', () => {
    const d = describeAuditEntry(
      entry({ action: 'user.deactivated', oldValues: '{"isActive":true}', newValues: '{"isActive":false}' }),
      { userNames: { 'u-2': 'Omar Ali' } },
    )
    expect(d.summary).toEqual({ key: 'userDeactivated', values: { name: 'Omar Ali' } })
  })

  it('shows settings changes without the response wrapper', () => {
    const d = describeAuditEntry(
      entry({
        action: 'settings.updated',
        entityType: 'SystemSettings',
        oldValues: '{"ticketPrefix":"TCK-"}',
        newValues: '{"response":{"ticketPrefix":"SUP-"},"secretsChanged":[]}',
      }),
    )
    expect(d.summary.key).toBe('settingsChanged')
    expect(d.diff).toContainEqual({ field: 'ticketPrefix', old: 'TCK-', new: 'SUP-', kind: 'changed' })
  })

  it('falls back to a generic sentence for an unknown action', () => {
    const d = describeAuditEntry(entry({ action: 'something.new' as AuditLogEntry['action'] }))
    expect(d.summary.key).toBe('generic')
  })
})

describe('formatIpAddress', () => {
  it.each([
    ['::1', { kind: 'local' }],
    ['127.0.0.1', { kind: 'local' }],
    ['::ffff:127.0.0.1', { kind: 'local' }],
    ['203.0.113.7', { kind: 'address', value: '203.0.113.7' }],
    ['::ffff:203.0.113.7', { kind: 'address', value: '203.0.113.7' }],
    [null, { kind: 'unknown' }],
    ['', { kind: 'unknown' }],
  ])('formats %s', (input, expected) => {
    expect(formatIpAddress(input)).toEqual(expected)
  })
})
