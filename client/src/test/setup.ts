import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

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

afterEach(() => {
  cleanup()
  localStorage.clear()
  // BrowserRouter reads the real URL: start every test at "/".
  window.history.replaceState(null, '', '/')
})
