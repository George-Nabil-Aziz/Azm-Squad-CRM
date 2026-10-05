import { Outlet } from 'react-router'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { AppHeader } from './AppHeader'
import { AppSidebar } from './AppSidebar'

/** App shell for signed-in pages: sidebar + header + the current page. */
export function AppLayout() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <AppHeader />
        <div className="flex flex-1 flex-col p-4 md:p-6">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
