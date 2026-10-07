import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { fakeApi, inOneHour } from './test/fake-api'
import { i18n } from './i18n/i18n'

const originalWidth = window.innerWidth

function useWidth(width: number) {
  Object.defineProperty(window, 'innerWidth', { writable: true, configurable: true, value: width })
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

describe('Mobile layout (375px)', () => {
  beforeEach(() => {
    saveSession('good-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    useWidth(375)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    useWidth(originalWidth)
  })

  it('collapses the sidebar into a menu: the navigation is hidden until the menu button is pressed', async () => {
    renderAt('/')
    await screen.findByRole('heading', { level: 1, name: 'Dashboard' })

    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Show or hide the sidebar' }))

    const dialog = await screen.findByRole('dialog')
    const navigation = within(dialog).getByRole('navigation', { name: 'Main navigation' })
    expect(await within(navigation).findByRole('link', { name: 'Users' })).toBeInTheDocument()
  })

  it('closes the menu after choosing a page', async () => {
    renderAt('/')
    await screen.findByRole('heading', { level: 1, name: 'Dashboard' })
    fireEvent.click(screen.getByRole('button', { name: 'Show or hide the sidebar' }))
    const dialog = await screen.findByRole('dialog')

    fireEvent.click(await within(dialog).findByRole('link', { name: 'Tickets' }))

    expect(window.location.pathname).toBe('/tickets')
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('works the same in Arabic (right-to-left): the menu opens on the right with the Arabic navigation', async () => {
    await i18n.changeLanguage('ar')
    renderAt('/')
    await screen.findByRole('heading', { level: 1, name: 'لوحة التحكم' })

    fireEvent.click(screen.getByRole('button', { name: 'إظهار القائمة الجانبية أو إخفاؤها' }))

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveAttribute('data-side', 'right')
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')
    expect(await within(dialog).findByRole('link', { name: 'المستخدمون' })).toBeInTheDocument()
  })

  it('keeps the header usable: the sign-out button stays, the user name is shown only from the sm breakpoint', async () => {
    renderAt('/')

    const name = await screen.findByText('System Administrator')
    expect(name).toHaveClass('max-sm:hidden')
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
  })
})
