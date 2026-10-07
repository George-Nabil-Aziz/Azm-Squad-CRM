import { useBranding } from './branding-context'

/** The company logo when one is set (nothing otherwise). `alt` is the company name, read by screen readers. */
export function BrandLogo({ alt, className }: { alt: string; className?: string }) {
  const { logoUrl } = useBranding()
  if (!logoUrl) return null
  return <img src={logoUrl} alt={alt} className={className ?? 'h-8 w-auto max-w-40 object-contain'} />
}
