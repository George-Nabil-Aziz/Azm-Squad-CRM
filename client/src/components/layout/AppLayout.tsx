import { useEffect, useState } from 'react'
import { Outlet } from 'react-router'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { TooltipProvider } from '@/components/ui/tooltip'
import { AppHeader } from './AppHeader'
import { AppSidebar } from './AppSidebar'

/** Tailwind's `lg` breakpoint: from here the sidebar is expanded by default, below it (tablet) icon-only. */
const DESKTOP_MIN_WIDTH = 1024
/** Cookie written by the shadcn SidebarProvider whenever the user toggles the sidebar. */
const SIDEBAR_COOKIE = 'sidebar_state'

const isDesktop = () => window.innerWidth >= DESKTOP_MIN_WIDTH

/** The user's last choice (cookie), else expanded on desktop and icon-only on tablet. */
function initialOpen(): boolean {
  const saved = document.cookie.split('; ').find((entry) => entry.startsWith(`${SIDEBAR_COOKIE}=`))
  return saved ? saved.split('=')[1] === 'true' : isDesktop()
}

/** App shell for signed-in pages: sidebar + header + the current page. */
export function AppLayout() {
  const [open, setOpen] = useState(initialOpen)

  // Crossing the desktop / tablet boundary resets the sidebar to that size's default (no cookie is written).
  useEffect(() => {
    let wasDesktop = isDesktop()
    const onResize = () => {
      const nowDesktop = isDesktop()
      if (nowDesktop !== wasDesktop) {
        wasDesktop = nowDesktop
        setOpen(nowDesktop)
      }
    }
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  return (
    <TooltipProvider>
      <SidebarProvider open={open} onOpenChange={setOpen}>
        <AppSidebar />
        <SidebarInset>
          <AppHeader />
          <div className="flex min-w-0 flex-1 flex-col p-4 md:p-6">
            <Outlet />
          </div>
        </SidebarInset>
      </SidebarProvider>
    </TooltipProvider>
  )
}
