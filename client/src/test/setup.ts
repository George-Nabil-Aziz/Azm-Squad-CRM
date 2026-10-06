import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { i18n } from '../i18n/i18n'

// No real SignalR connection in tests: the notification hub is replaced (tests that need pushes mock it themselves).
vi.mock('@/api/notifications-hub', () => ({ connectNotificationsHub: () => () => {} }))

// jsdom has no matchMedia. shadcn's sidebar (useIsMobile) and sonner's "system" theme call it.
// matches: false → desktop layout, light theme.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  configurable: true,
  value: (query: string): MediaQueryList => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }),
})

// jsdom has no ResizeObserver. Radix Checkbox (inside a <form>) measures itself with it.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
Object.defineProperty(window, 'ResizeObserver', { writable: true, configurable: true, value: ResizeObserverStub })

afterEach(async () => {
  cleanup()
  localStorage.clear()
  // BrowserRouter reads the real URL: start every test at "/".
  window.history.replaceState(null, '', '/')
  // Every test starts in English (also resets <html lang="en" dir="ltr">).
  await i18n.changeLanguage('en')
})
