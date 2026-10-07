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

  it('is light on first visit when the system is light, and offers dark', () => {
    renderToggle()
    expect(root).not.toHaveClass('dark')
    expect(screen.getByRole('button', { name: 'Switch to dark mode' })).toHaveAttribute('aria-pressed', 'false')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull()
  })

  it('is dark on first visit when the system is dark, and offers light', () => {
    mockSystemDark(true)
    renderToggle()
    expect(root).toHaveClass('dark')
    expect(screen.getByRole('button', { name: 'Switch to light mode' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('switches to dark and back to light, toggling the class and saving the choice', () => {
    renderToggle()

    fireEvent.click(screen.getByRole('button', { name: 'Switch to dark mode' }))
    expect(root).toHaveClass('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')

    fireEvent.click(screen.getByRole('button', { name: 'Switch to light mode' }))
    expect(root).not.toHaveClass('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('restores the saved choice, even against the system preference', () => {
    mockSystemDark(true)
    localStorage.setItem(THEME_STORAGE_KEY, 'light')
    renderToggle()
    expect(root).not.toHaveClass('dark')
    expect(screen.getByRole('button', { name: 'Switch to dark mode' })).toBeInTheDocument()
  })

  it.each(['system', 'purple'])('treats a saved "%s" as nothing saved', (value) => {
    mockSystemDark(true)
    localStorage.setItem(THEME_STORAGE_KEY, value)
    renderToggle()
    expect(root).toHaveClass('dark')
  })

  it('still works when localStorage throws', () => {
    const get = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    const set = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    renderToggle()
    fireEvent.click(screen.getByRole('button', { name: 'Switch to dark mode' }))
    expect(root).toHaveClass('dark')
    get.mockRestore()
    set.mockRestore()
  })

  it('has Arabic labels', async () => {
    await i18n.changeLanguage('ar')
    renderToggle()
    expect(screen.getByRole('button', { name: 'التبديل إلى الوضع الداكن' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button'))
    expect(screen.getByRole('button', { name: 'التبديل إلى الوضع الفاتح' })).toBeInTheDocument()
  })
})
