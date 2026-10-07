import { useTranslation } from 'react-i18next'
import { SettingsForm } from '@/features/settings/SettingsForm'
import { useSettings } from '@/features/settings/useSettings'

/** System configuration (SuperAdmin): business hours, time zone, ticket number prefix, email and WhatsApp credentials. */
export function SettingsPage() {
  const { t } = useTranslation()
  const settings = useSettings()

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-primary">{t('nav.settings')}</h1>
        <p className="text-muted-foreground">{t('settings.description')}</p>
      </div>

      {settings.isPending ? (
        <p className="text-muted-foreground">{t('settings.loading')}</p>
      ) : settings.data ? (
        <SettingsForm settings={settings.data} />
      ) : null}
    </div>
  )
}
