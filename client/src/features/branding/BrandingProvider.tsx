import { useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, type ReactNode } from 'react'
import { getBranding } from '@/api/branding'
import { applyBrandColors, BrandingContext, defaultBranding } from './branding-context'

/**
 * Fetches the public branding once (GET /api/branding, needs no sign-in) and applies the colours to the theme variables;
 * the logo is read through useBranding(). A failed request leaves the default theme.
 */
export function BrandingProvider({ children }: { children: ReactNode }) {
  const query = useQuery({
    queryKey: ['branding'],
    queryFn: ({ signal }) => getBranding(signal),
    staleTime: 5 * 60_000,
    retry: false,
  })
  const branding = useMemo(() => query.data ?? defaultBranding, [query.data])

  useEffect(() => {
    applyBrandColors(document.documentElement, branding)
    return () => applyBrandColors(document.documentElement, defaultBranding)
  }, [branding])

  return <BrandingContext.Provider value={branding}>{children}</BrandingContext.Provider>
}
