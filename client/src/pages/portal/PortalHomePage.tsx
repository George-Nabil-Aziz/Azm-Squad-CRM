import { useTranslation } from 'react-i18next'

/** Landing page of the portal (the help center content arrives with CRM-43). */
export function PortalHomePage() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-2">
      <h1 className="text-2xl font-semibold">{t('portal.home.title')}</h1>
      <p className="text-muted-foreground">{t('portal.home.description')}</p>
    </div>
  )
}
