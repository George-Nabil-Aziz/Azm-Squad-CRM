import { describe, expect, it } from 'vitest'
import ar from './ar.json'
import en from './en.json'

/** "a.b.c" → value, for every leaf string of a translation file. */
function flatten(tree: object, prefix = ''): Record<string, string> {
  return Object.entries(tree).reduce<Record<string, string>>((all, [key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return typeof value === 'string' ? { ...all, [path]: value } : { ...all, ...flatten(value as object, path) }
  }, {})
}

const english = flatten(en)
const arabic = flatten(ar)

/** Keys whose Arabic value is intentionally not Arabic: the switch shows the *other* language's name. */
const NOT_ARABIC_IN_AR = ['language.switch', 'quickReplies.samples.ticketNumber']

const placeholders = (text: string) => (text.match(/\{\{\s*\w+\s*\}\}/g) ?? []).sort()

describe('translation files', () => {
  it('ar.json and en.json have exactly the same keys', () => {
    expect(Object.keys(arabic).sort()).toEqual(Object.keys(english).sort())
  })

  it.each(Object.keys(english))('"%s" is not empty in either language', (key) => {
    expect(english[key].trim()).not.toBe('')
    expect(arabic[key]?.trim()).not.toBe('')
  })

  it.each(Object.keys(english).filter((key) => !NOT_ARABIC_IN_AR.includes(key)))(
    '"%s" is Arabic text in ar.json',
    (key) => {
      expect(arabic[key]).toMatch(/[؀-ۿ]/)
    },
  )

  it.each(Object.keys(english))('"%s" uses the same {{placeholders}} in both languages', (key) => {
    expect(placeholders(arabic[key] ?? '')).toEqual(placeholders(english[key]))
  })
})
