import { createContext, useContext } from 'react'

export type ThemePreference = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

/** localStorage key of the chosen theme; client/index.html reads the same key before the first paint. */
export const THEME_STORAGE_KEY = 'crm-theme'

export const THEME_PREFERENCES: readonly ThemePreference[] = ['light', 'dark', 'system']

export interface ThemeContextValue {
  /** What the user picked (system = follow the operating system). */
  theme: ThemePreference
  /** What is actually shown. */
  resolvedTheme: ResolvedTheme
  setTheme: (theme: ThemePreference) => void
}

export const ThemeContext = createContext<ThemeContextValue>({
  theme: 'system',
  resolvedTheme: 'light',
  setTheme: () => {},
})

/** The theme choice and a setter; the system default outside a ThemeProvider. */
export function useTheme(): ThemeContextValue {
  return useContext(ThemeContext)
}

export function isThemePreference(value: unknown): value is ThemePreference {
  return value === 'light' || value === 'dark' || value === 'system'
}

/** Saved choice, or "system" when nothing (valid) is saved or storage is unavailable. */
export function readStoredTheme(): ThemePreference {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY)
    return isThemePreference(value) ? value : 'system'
  } catch {
    return 'system'
  }
}

export function storeTheme(theme: ThemePreference) {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme)
  } catch {
    // Private mode or blocked storage: the choice just lasts until the page is closed.
  }
}

export const DARK_QUERY = '(prefers-color-scheme: dark)'

export function systemPrefersDark(): boolean {
  try {
    return window.matchMedia(DARK_QUERY).matches
  } catch {
    return false
  }
}

export function resolveTheme(theme: ThemePreference): ResolvedTheme {
  if (theme === 'system') return systemPrefersDark() ? 'dark' : 'light'
  return theme
}

/** Adds or removes the `dark` class on <html> (the Tailwind dark variant) and sets the browser's color-scheme. */
export function applyTheme(resolved: ResolvedTheme) {
  const root = document.documentElement
  root.classList.toggle('dark', resolved === 'dark')
  root.style.colorScheme = resolved
}
