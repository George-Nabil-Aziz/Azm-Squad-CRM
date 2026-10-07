import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

interface CrmLogoProps {
  /** `mark` = the symbol only (collapsed sidebar); `full` = symbol + product name. */
  variant?: 'mark' | 'full'
  /** Accessible name of the logo; empty = decorative (the product name is already next to it). */
  alt?: string
  className?: string
}

/**
 * The default CRM logo: a speech bubble with a check mark (support, resolved). Drawn with `currentColor`
 * (the brand primary through `text-primary`), so it follows the branding colour in light and dark.
 */
export function CrmLogo({ variant = 'mark', alt = '', className }: CrmLogoProps) {
  const { t } = useTranslation()
  const mark = (
    <svg
      viewBox="0 0 32 32"
      className={cn('size-8 shrink-0 text-primary', variant === 'mark' && className)}
      role={alt ? 'img' : undefined}
      aria-label={alt || undefined}
      aria-hidden={alt ? undefined : true}
      data-testid="crm-logo"
      fill="none"
    >
      <mask id="crm-logo-cutout" maskUnits="userSpaceOnUse" x="0" y="0" width="32" height="32">
        <rect width="32" height="32" fill="white" />
        <path d="m9.5 14.5 4.2 4.2 8.8-8.8" stroke="black" strokeWidth="2.8" strokeLinecap="round" strokeLinejoin="round" />
      </mask>
      <path
        mask="url(#crm-logo-cutout)"
        d="M6 4h20a4 4 0 0 1 4 4v13a4 4 0 0 1-4 4H15l-6.5 5.2a.8.8 0 0 1-1.3-.6V25H6a4 4 0 0 1-4-4V8a4 4 0 0 1 4-4Z"
        fill="currentColor"
      />
    </svg>
  )
  if (variant === 'mark') return mark
  return (
    <span className={cn('inline-flex items-center gap-2 font-semibold', className)}>
      {mark}
      <span>{t('app.name')}</span>
    </span>
  )
}
