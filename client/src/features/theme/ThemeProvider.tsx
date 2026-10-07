import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  applyTheme,
  DARK_QUERY,
  readStoredTheme,
  resolveTheme,
  storeTheme,
  ThemeContext,
  type ThemePreference,
} from './theme-context'

/**
 * Light / dark / system theme. Keeps the choice in localStorage and mirrors it as the `dark` class on <html>;
 * client/index.html sets the same class before React loads, so there is no flash of the wrong theme.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemePreference>(readStoredTheme)
  const [resolvedTheme, setResolvedTheme] = useState(() => resolveTheme(theme))

  useEffect(() => {
    const update = () => {
      const resolved = resolveTheme(theme)
      setResolvedTheme(resolved)
      applyTheme(resolved)
    }
    update()
    if (theme !== 'system') return
    // Follow the operating system while "system" is chosen.
    let query: MediaQueryList
    try {
      query = window.matchMedia(DARK_QUERY)
    } catch {
      return
    }
    query.addEventListener('change', update)
    return () => query.removeEventListener('change', update)
  }, [theme])

  // Another tab changed the choice.
  useEffect(() => {
    const onStorage = () => setThemeState(readStoredTheme())
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [])

  const setTheme = useCallback((next: ThemePreference) => {
    storeTheme(next)
    setThemeState(next)
  }, [])

  const value = useMemo(() => ({ theme, resolvedTheme, setTheme }), [theme, resolvedTheme, setTheme])
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
