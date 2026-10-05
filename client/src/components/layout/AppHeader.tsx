import { LogOutIcon } from 'lucide-react'
import { signOut } from '@/auth/sign-in'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { authMessages } from '@/features/auth/auth-messages'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Top bar: sidebar toggle, signed-in user, sign out. Sign out clears the token; RequireAuth then redirects to /login. */
export function AppHeader() {
  const { data: user } = useCurrentUser()

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger className="-ms-1" />
      <Separator orientation="vertical" className="me-2 data-[orientation=vertical]:h-4" />
      <div className="ms-auto flex items-center gap-3">
        {user ? <span className="text-sm text-muted-foreground">{user.fullName}</span> : null}
        <Button variant="outline" size="sm" onClick={signOut}>
          <LogOutIcon aria-hidden="true" />
          {authMessages.signOut}
        </Button>
      </div>
    </header>
  )
}
