import { beforeEach, describe, expect, it, vi } from 'vitest'

// Every test loads a fresh copy of the module, like a page reload: the language is read from storage at start-up.
async function loadI18n() {
  vi.resetModules()
  return import('./i18n')
}

describe('i18n start-up and language persistence', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('starts in English with <html dir="ltr" lang="en"> when nothing is saved', async () => {
    const { i18n } = await loadI18n()

    expect(i18n.language).toBe('en')
    expect(document.documentElement).toHaveAttribute('lang', 'en')
    expect(document.documentElement).toHaveAttribute('dir', 'ltr')
  })

  it('starts in the saved language after a reload', async () => {
    localStorage.setItem('crm.language', 'ar')

    const { i18n } = await loadI18n()

    expect(i18n.language).toBe('ar')
    expect(i18n.t('auth.signIn')).toBe('تسجيل الدخول')
    expect(document.documentElement).toHaveAttribute('lang', 'ar')
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')
  })

  it.each(['fr', '', 'AR', '{"x":1}'])('ignores an unsupported saved value %j and starts in English', async (saved) => {
    localStorage.setItem('crm.language', saved)

    const { i18n } = await loadI18n()

    expect(i18n.language).toBe('en')
  })

  it('setLanguage switches the language, saves it and updates <html lang dir>', async () => {
    const { i18n, setLanguage, getLanguage } = await loadI18n()

    await setLanguage('ar')

    expect(getLanguage()).toBe('ar')
    expect(i18n.t('nav.dashboard')).toBe('لوحة التحكم')
    expect(localStorage.getItem('crm.language')).toBe('ar')
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')

    await setLanguage('en')

    expect(localStorage.getItem('crm.language')).toBe('en')
    expect(document.documentElement).toHaveAttribute('lang', 'en')
    expect(document.documentElement).toHaveAttribute('dir', 'ltr')
  })
})
