import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { i18n } from './i18n/i18n'
import { fakeApi, inOneHour } from './test/fake-api'

const originalWidth = window.innerWidth

function useWidth(width: number) {
  Object.defineProperty(window, 'innerWidth', { writable: true, configurable: true, value: width })
}

function clearSidebarCookie() {
  document.cookie = 'sidebar_state=; path=/; max-age=0'
}

async function renderShell() {
  window.history.replaceState(null, '', '/')
  const view = render(<App />)
  await screen.findByRole('heading', { level: 1, name: /Dashboard|لوحة التحكم/ })
  return view
}

const sidebar = () => document.querySelector('[data-slot="sidebar"]') as HTMLElement
const toggle = () => screen.getByRole('button', { name: /Show or hide the sidebar|إظهار القائمة الجانبية أو إخفاؤها/ })

describe('Responsive sidebar', () => {
  beforeEach(async () => {
    clearSidebarCookie()
    saveSession('good-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    await i18n.changeLanguage('en')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    useWidth(originalWidth)
    clearSidebarCookie()
  })

  it('desktop: starts expanded and the header trigger toggles icon-only mode', async () => {
    useWidth(1280)
    await renderShell()
    expect(sidebar()).toHaveAttribute('data-state', 'expanded')

    fireEvent.click(toggle())
    expect(sidebar()).toHaveAttribute('data-state', 'collapsed')
    expect(sidebar()).toHaveAttribute('data-collapsible', 'icon')

    fireEvent.click(toggle())
    expect(sidebar()).toHaveAttribute('data-state', 'expanded')
  })

  it('desktop: remembers the choice across visits', async () => {
    useWidth(1280)
    const first = await renderShell()
    fireEvent.click(toggle())
    first.unmount()

    await renderShell()
    expect(sidebar()).toHaveAttribute('data-collapsible', 'icon')
  })

  it('icon mode: links keep their names and show a tooltip with the item name', async () => {
    useWidth(1280)
    await renderShell()
    fireEvent.click(toggle())

    const link = await screen.findByRole('link', { name: 'Tickets' })
    fireEvent.focus(link)
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Tickets')
  })

  it('tablet: icon-only by default, and can be expanded', async () => {
    useWidth(900)
    await renderShell()
    expect(sidebar()).toHaveAttribute('data-collapsible', 'icon')

    fireEvent.click(toggle())
    expect(sidebar()).toHaveAttribute('data-state', 'expanded')
  })

  it('follows the screen when it is resized between tablet and desktop', async () => {
    useWidth(1280)
    await renderShell()
    expect(sidebar()).toHaveAttribute('data-state', 'expanded')

    act(() => {
      useWidth(900)
      window.dispatchEvent(new Event('resize'))
    })
    expect(sidebar()).toHaveAttribute('data-collapsible', 'icon')

    act(() => {
      useWidth(1280)
      window.dispatchEvent(new Event('resize'))
    })
    expect(sidebar()).toHaveAttribute('data-state', 'expanded')
  })

  it('mobile: no sidebar in the page, it opens as a sheet from the header trigger', async () => {
    useWidth(375)
    await renderShell()
    expect(sidebar()).toBeNull()

    fireEvent.click(toggle())
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })

  it('Arabic: the sidebar is on the right, also in icon mode', async () => {
    await i18n.changeLanguage('ar')
    useWidth(1280)
    await renderShell()
    expect(sidebar()).toHaveAttribute('data-side', 'right')

    fireEvent.click(toggle())
    expect(sidebar()).toHaveAttribute('data-side', 'right')
    expect(sidebar()).toHaveAttribute('data-collapsible', 'icon')

    fireEvent.focus(await screen.findByRole('link', { name: 'المستخدمون' }))
    expect(await screen.findByRole('tooltip')).toHaveTextContent('المستخدمون')
  })
})
