import { useTranslation } from 'react-i18next'
import type { NavigationId } from '@/app/navigation'

/** Placeholder for a sidebar area whose story is not built yet (keeps every navigation link working). */
export function ComingSoonPage({ area }: { area: NavigationId }) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-1">
      <h1 className="text-2xl font-semibold">{t(`nav.${area}`)}</h1>
      <p className="text-muted-foreground">{t('shell.comingSoon')}</p>
    </div>
  )
}
