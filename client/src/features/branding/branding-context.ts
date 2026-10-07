import { createContext, useContext } from 'react'
import type { Branding } from '@/api/branding'
import { contrastForeground } from './branding-colors'

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

/** Applies the brand colours to the theme variables of `root` (null = back to the theme default). */
export function applyBrandColors(root: HTMLElement, branding: Pick<Branding, 'primaryColor' | 'secondaryColor'>) {
  apply(root, primaryVariables, branding.primaryColor)
  apply(root, primaryForegroundVariables, branding.primaryColor ? contrastForeground(branding.primaryColor) : null)
  apply(root, secondaryVariables, branding.secondaryColor)
  apply(root, secondaryForegroundVariables, branding.secondaryColor ? contrastForeground(branding.secondaryColor) : null)
}

