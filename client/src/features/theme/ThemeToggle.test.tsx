import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { i18n } from '@/i18n/i18n'
import { ThemeProvider } from './ThemeProvider'
import { ThemeToggle } from './ThemeToggle'
import { THEME_STORAGE_KEY } from './theme-context'

const originalMatchMedia = window.matchMedia

function mockSystemDark(dark: boolean) {
  window.matchMedia = ((query: string) => ({
    matches: dark && query.includes('dark'),
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  })) as typeof window.matchMedia
}

function renderToggle() {
  return render(
    <ThemeProvider>
      <ThemeToggle />
    </ThemeProvider>,
  )
}

const root = document.documentElement

describe('ThemeToggle', () => {
  beforeEach(() => {
    root.classList.remove('dark')
    mockSystemDark(false)
  })
  afterEach(() => {
    window.matchMedia = originalMatchMedia
    root.classList.remove('dark')
  })

  it('follows the system preference on first visit (light)', () => {
    renderToggle()
    expect(root).not.toHaveClass('dark')
    expect(screen.getByRole('radio', { name: 'System' })).toBeChecked()
  })

  it('follows the system preference on first visit (dark)', () => {
    mockSystemDark(true)
    renderToggle()
    expect(root).toHaveClass('dark')
    expect(screen.getByRole('radio', { name: 'System' })).toBeChecked()
  })

  it('switches to dark and back to light, toggling the class and saving the choice', () => {
    renderToggle()

    fireEvent.click(screen.getByRole('radio', { name: 'Dark' }))
    expect(root).toHaveClass('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(screen.getByRole('radio', { name: 'Dark' })).toBeChecked()

    fireEvent.click(screen.getByRole('radio', { name: 'Light' }))
    expect(root).not.toHaveClass('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('restores the saved choice, even against the system preference', () => {
    mockSystemDark(true)
    localStorage.setItem(THEME_STORAGE_KEY, 'light')
    renderToggle()
    expect(root).not.toHaveClass('dark')
    expect(screen.getByRole('radio', { name: 'Light' })).toBeChecked()
  })

  it('ignores an invalid saved value', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'purple')
    renderToggle()
    expect(screen.getByRole('radio', { name: 'System' })).toBeChecked()
  })

  it('still works when localStorage throws', () => {
    const get = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    const set = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    renderToggle()
    fireEvent.click(screen.getByRole('radio', { name: 'Dark' }))
    expect(root).toHaveClass('dark')
    get.mockRestore()
    set.mockRestore()
  })

  it('has Arabic labels', async () => {
    await i18n.changeLanguage('ar')
    renderToggle()
    expect(screen.getByRole('radiogroup', { name: 'المظهر' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'فاتح' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'داكن' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'النظام' })).toBeInTheDocument()
  })
})
