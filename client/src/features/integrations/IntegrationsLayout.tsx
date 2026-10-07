import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { cn } from '@/lib/utils'

/** Sub navigation of the integrations area (CRM-58..60); every section is a child route. */
const sections = [
  { id: 'apiKeys', path: '/integrations/api-keys' },
  { id: 'webhooks', path: '/integrations/webhooks' },
] as const

export function IntegrationsLayout() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.integrations')}</h1>
        <p className="text-muted-foreground">{t('integrations.description')}</p>
      </div>
      <nav aria-label={t('integrations.navigation')} className="flex flex-wrap gap-1 border-b">
        {sections.map((section) => (
          <NavLink
            key={section.id}
            to={section.path}
            className={({ isActive }) =>
              cn(
                'border-b-2 px-3 py-2 text-sm font-medium',
                isActive ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground',
              )
            }
          >
            {t(`integrations.nav.${section.id}`)}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </div>
  )
}
