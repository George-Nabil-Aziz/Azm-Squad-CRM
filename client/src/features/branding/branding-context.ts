import { createContext, useContext } from 'react'
import type { Branding } from '@/api/branding'
import type { ResolvedTheme } from '@/features/theme/theme-context'
import { contrastForeground, DARK_FOREGROUND, isTooDarkForDarkMode } from './branding-colors'

export const defaultBranding: Branding = { primaryColor: null, secondaryColor: null, logoUrl: null }

export const BrandingContext = createContext<Branding>(defaultBranding)

/** The branding (colours and logo); the default (no logo, theme colours) outside a BrandingProvider or while it loads. */
export function useBranding(): Branding {
  return useContext(BrandingContext)
}

/** Theme variables a brand colour overrides: the CSS variables of the shadcn theme (set on <html>, no rebuild needed). */
const primaryVariables = ['--primary', '--ring', '--sidebar-primary'] as const
const primaryForegroundVariables = ['--primary-foreground', '--sidebar-primary-foreground'] as const
const secondaryVariables = ['--secondary'] as const
const secondaryForegroundVariables = ['--secondary-foreground'] as const

function apply(root: HTMLElement, variables: readonly string[], value: string | null) {
  for (const name of variables) {
    if (value) root.style.setProperty(name, value)
    else root.style.removeProperty(name)
  }
}

/**
 * Applies the brand colours to the theme variables of `root` (null = back to the theme default).
 *
 * Light theme: both brand colours are used as they are. Dark theme: the primary is kept (lightened with white when it
 * would vanish on the dark surfaces, with dark text on top) and the secondary is skipped, so the dark palette keeps
 * its quiet surfaces and every text stays readable.
 */
export function applyBrandColors(
  root: HTMLElement,
  branding: Pick<Branding, 'primaryColor' | 'secondaryColor'>,
  theme: ResolvedTheme = 'light',
) {
  const { primaryColor, secondaryColor } = branding
  const dark = theme === 'dark'
  const lighten = dark && primaryColor !== null && isTooDarkForDarkMode(primaryColor)
  const primary = primaryColor && lighten ? `color-mix(in oklch, ${primaryColor}, white 55%)` : primaryColor
  const primaryForeground = primaryColor ? (lighten ? DARK_FOREGROUND : contrastForeground(primaryColor)) : null
  const secondary = dark ? null : secondaryColor

  apply(root, primaryVariables, primary)
  apply(root, primaryForegroundVariables, primaryForeground)
  apply(root, secondaryVariables, secondary)
  apply(root, secondaryForegroundVariables, secondary ? contrastForeground(secondary) : null)
}

