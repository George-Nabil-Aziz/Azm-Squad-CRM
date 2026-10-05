/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { permissions } from './permissions'

// The server's catalogue is the source of truth; this list must name exactly the same permissions.
const serverSource = readFileSync(
  join(import.meta.dirname, '../../../server/src/Crm.Application/Auth/Permissions.cs'),
  'utf8',
)

describe('permissions', () => {
  it('lists exactly the permissions of the server catalogue', () => {
    const serverPermissions = [...serverSource.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1])

    expect(serverPermissions.length).toBeGreaterThan(0)
    expect(Object.values(permissions).toSorted()).toEqual(serverPermissions.toSorted())
  })
})
