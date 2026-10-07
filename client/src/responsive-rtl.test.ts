/// <reference types="node" />
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

/** Every .tsx file of the app that is not a test and not generated shadcn code. */
function componentFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return path.includes(join('components', 'ui')) ? [] : componentFiles(path)
    return name.endsWith('.tsx') && !name.endsWith('.test.tsx') ? [path] : []
  })
}

const files = componentFiles(import.meta.dirname).map((path) => ({ path, source: readFileSync(path, 'utf8') }))

// Tailwind classes that only make sense for one reading direction (CLAUDE.md: use ms-/me-/ps-/pe-/start-/end- instead).
const PHYSICAL = /(^|[\s"'`:])(ml|mr|pl|pr|left|right|-ml|-mr)-[\w[]|text-(left|right)\b|(rounded|border)-(l|r|tl|tr|bl|br)(-|\s|"|'|`)/
// A fixed pixel width wider than a 375px phone screen, not limited to larger screens (min-w / w / max-w arbitrary values).
const WIDE_FIXED = /(^|[\s"'`])(min-w|w)-\[(\d+)px\]/g

describe('responsive and RTL guards', () => {
  it('finds the app source', () => {
    expect(files.length).toBeGreaterThan(50)
  })

  it('uses no direction-specific Tailwind classes in components (mobile layout must also work in Arabic)', () => {
    const offenders = files.filter(({ source }) => PHYSICAL.test(source)).map(({ path }) => path)

    expect(offenders).toEqual([])
  })

  it('sets no fixed pixel width that would overflow a 375px screen', () => {
    const offenders = files
      .filter(({ source }) => [...source.matchAll(WIDE_FIXED)].some((match) => Number(match[3]) > 340))
      .map(({ path }) => path)

    expect(offenders).toEqual([])
  })
})
