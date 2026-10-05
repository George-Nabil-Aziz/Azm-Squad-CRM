import { describe, expect, it } from 'vitest'
import en from './i18n/en.json'

// Every app source file as text (tests, test helpers, translation files and generated shadcn components excluded).
const sources = import.meta.glob<string>(
  ['./**/*.{ts,tsx}', '!./**/*.test.{ts,tsx}', '!./test/**', '!./i18n/**', '!./components/ui/**'],
  { query: '?raw', import: 'default', eager: true },
)
const components = Object.entries(sources).filter(([path]) => path.endsWith('.tsx'))

const LETTER = '[A-Za-z\\u0600-\\u06FF]'
/** Text between a closing `>` and the next `<` that contains a letter and no code characters. */
const JSX_TEXT = new RegExp(`>([^<>{}()=;]*${LETTER}[^<>{}()=;]*)<`, 'g')
/** Literal (not `{t(...)}`) values of attributes users see or hear. */
const TEXT_ATTRIBUTE = new RegExp(`\\b(?:aria-label|aria-description|title|placeholder|alt)="[^"]*${LETTER}`, 'g')

/** User-facing text written directly in a component instead of coming from t(). */
function findHardCodedText(source: string): string[] {
  const jsxText = [...source.matchAll(JSX_TEXT)].map((match) => match[1].trim())
  const attributes = [...source.matchAll(TEXT_ATTRIBUTE)].map((match) => match[0])
  return [...jsxText, ...attributes]
}

/** Every English UI string, e.g. "Sign in" (values with {{placeholders}} are skipped). */
function englishValues(tree: object): string[] {
  return Object.values(tree).flatMap((value) =>
    typeof value === 'string' ? (value.includes('{{') ? [] : [value]) : englishValues(value as object),
  )
}

describe('no hard-coded user-facing text (CLAUDE.md: all strings in i18n files)', () => {
  it('detects hard-coded text (self-check of the guard)', () => {
    expect(findHardCodedText('<p>Hello</p>')).toEqual(['Hello'])
    expect(findHardCodedText('<p>\n  مرحبا\n</p>')).toEqual(['مرحبا'])
    expect(findHardCodedText('<Button aria-label="Close">')).toEqual(['aria-label="Close'])
    expect(findHardCodedText('<p>{t(\'shell.comingSoon\')}</p>')).toEqual([])
    expect(findHardCodedText('<Icon aria-hidden="true" className="size-4" />')).toEqual([])
    expect(findHardCodedText('const [x] = useState<string | null>(null)')).toEqual([])
  })

  it('scans the app components', () => {
    expect(components.map(([path]) => path)).toContain('./components/layout/AppHeader.tsx')
  })

  it('has no text written directly in JSX or in aria-label/title/placeholder/alt', () => {
    const offenders = components
      .map(([path, source]) => [path, findHardCodedText(source)] as const)
      .filter(([, found]) => found.length > 0)
    expect(offenders).toEqual([])
  })

  it('keeps every English UI string only in i18n/en.json (no string literal copies in the code)', () => {
    const offenders = englishValues(en)
      .filter((value) => value.length > 3)
      .flatMap((value) =>
        Object.entries(sources)
          .filter(([, source]) => [`'${value}'`, `"${value}"`, `\`${value}\``].some((quoted) => source.includes(quoted)))
          .map(([path]) => `${path}: ${value}`),
      )
    expect(offenders).toEqual([])
  })
})
