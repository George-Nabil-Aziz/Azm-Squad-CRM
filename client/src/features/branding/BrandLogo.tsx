import { CrmLogo } from '@/components/brand/CrmLogo'
import { useBranding } from './branding-context'

/** The company logo when one is uploaded, the default CRM logo (themed SVG mark) otherwise. `alt` is read by screen readers ('' = decorative). */
export function BrandLogo({ alt, className }: { alt: string; className?: string }) {
  const { logoUrl } = useBranding()
  return logoUrl ? (
    <img src={logoUrl} alt={alt} className={className ?? 'h-8 w-auto max-w-40 object-contain'} />
  ) : (
    <CrmLogo alt={alt} className={className} />
  )
}
