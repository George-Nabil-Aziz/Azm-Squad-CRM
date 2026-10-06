import { LogOutIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { signOut } from '@/auth/sign-in'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { useCurrentUser } from '@/features/auth/useCurrentUser'
import { NotificationBell } from '@/features/notifications/NotificationBell'

/** Top bar: sidebar toggle, signed-in user, language switch, sign out. Sign out clears the token; RequireAuth then redirects to /login. */
export function AppHeader() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      {/* aria-label replaces the English sr-only text inside the generated shadcn component. */}
      <SidebarTrigger className="-ms-1" aria-label={t('shell.toggleSidebar')} />
      <Separator orientation="vertical" className="me-2 data-[orientation=vertical]:h-4" />
      <div className="ms-auto flex items-center gap-3">
        {user ? <span className="text-sm text-muted-foreground">{user.fullName}</span> : null}
        <NotificationBell />
        <LanguageSwitcher />
        <Button variant="outline" size="sm" onClick={signOut}>
          <LogOutIcon aria-hidden="true" />
          {t('auth.signOut')}
        </Button>
      </div>
    </header>
  )
}
