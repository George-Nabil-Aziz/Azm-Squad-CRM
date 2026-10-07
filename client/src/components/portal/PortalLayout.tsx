import { useQuery } from '@tanstack/react-query'
import { LogInIcon, LogOutIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, NavLink, Outlet } from 'react-router'
import { getChatbotStatus } from '@/api/portal-chatbot'
import { PORTAL_LOGIN_PATH } from '@/app/portal-paths'
import { clearPortalSession } from '@/auth/portal-session'
import { useIsPortalAuthenticated, usePortalCustomer } from '@/auth/usePortalSession'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { Button } from '@/components/ui/button'
import { BrandLogo } from '@/features/branding/BrandLogo'
import { ThemeToggle } from '@/features/theme/ThemeToggle'

/** Own simple layout of the customer portal (not the staff sidebar): header with navigation, language switch and sign in / out. */
export function PortalLayout() {
  const { t } = useTranslation()
  const isAuthenticated = useIsPortalAuthenticated()
  const customer = usePortalCustomer()
  // The chat link only shows while the server has an AI key (CRM-54).
  const chatbot = useQuery({ queryKey: ['portal-chatbot', 'status'], queryFn: ({ signal }) => getChatbotStatus(signal) })

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `rounded-md px-3 py-1.5 text-sm font-medium ${isActive ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:bg-muted hover:text-foreground'}`

  return (
    <div className="flex min-h-svh flex-col bg-background">
      <header className="sticky top-0 z-10 border-b bg-background/80 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
          <Link to="/portal" className="flex items-center gap-2 text-lg font-semibold">
            <BrandLogo alt="" />
            {t('portal.title')}
          </Link>
          <nav aria-label={t('portal.navigation')} className="flex flex-wrap gap-1">
            <NavLink to="/portal" end className={linkClass}>
              {t('portal.nav.help')}
            </NavLink>
            {chatbot.data?.enabled ? (
              <NavLink to="/portal/chat" className={linkClass}>
                {t('portal.nav.chat')}
              </NavLink>
            ) : null}
            {isAuthenticated ? (
              <>
                <NavLink to="/portal/tickets" end className={linkClass}>
                  {t('portal.nav.tickets')}
                </NavLink>
                <NavLink to="/portal/tickets/new" className={linkClass}>
                  {t('portal.nav.newTicket')}
                </NavLink>
              </>
            ) : null}
          </nav>
          <div className="ms-auto flex items-center gap-3">
            {customer ? <span className="text-sm text-muted-foreground">{customer.name}</span> : null}
            <ThemeToggle />
            <LanguageSwitcher />
            {isAuthenticated ? (
              <Button variant="outline" size="sm" onClick={clearPortalSession}>
                <LogOutIcon aria-hidden="true" />
                {t('portal.signOut')}
              </Button>
            ) : (
              <Button variant="outline" size="sm" asChild>
                <Link to={PORTAL_LOGIN_PATH}>
                  <LogInIcon aria-hidden="true" />
                  {t('portal.signIn')}
                </Link>
              </Button>
            )}
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
