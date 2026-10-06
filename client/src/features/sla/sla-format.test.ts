import { afterEach, describe, expect, it } from 'vitest'
import { i18n } from '@/i18n/i18n'
import { formatMinutes } from './sla-format'

describe('formatMinutes', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('shows minutes, hours, or both', () => {
    const t = i18n.getFixedT('en')

    expect(formatMinutes(45, t)).toBe('45 min')
    expect(formatMinutes(60, t)).toBe('1 h')
    expect(formatMinutes(90, t)).toBe('1 h 30 min')
    expect(formatMinutes(4320, t)).toBe('72 h')
  })

  it('has Arabic units', () => {
    const t = i18n.getFixedT('ar')

    expect(formatMinutes(90, t)).toBe('1 س 30 د')
  })
})
