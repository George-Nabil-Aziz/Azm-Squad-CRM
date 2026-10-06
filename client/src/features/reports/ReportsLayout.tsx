import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { cn } from '@/lib/utils'

/** Sub navigation of the reports area; every report is a child route. */
const reportLinks = [{ id: 'tickets', path: '/reports/tickets' }] as const

export function ReportsLayout() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.reports')}</h1>
        <p className="text-muted-foreground">{t('reports.description')}</p>
      </div>
      <nav aria-label={t('reports.navigation')} className="flex flex-wrap gap-1 border-b">
        {reportLinks.map((link) => (
          <NavLink
            key={link.id}
            to={link.path}
            className={({ isActive }) =>
              cn(
                'border-b-2 px-3 py-2 text-sm font-medium',
                isActive ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground',
              )
            }
          >
            {t(`reports.nav.${link.id}`)}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </div>
  )
}
