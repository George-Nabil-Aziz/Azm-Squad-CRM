import { NavLink, useMatch } from 'react-router'
import { shellMessages } from '@/app/messages'
import { navigationItems, type NavigationItem } from '@/app/navigation'
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'

function AppSidebarLink({ item }: { item: NavigationItem }) {
  const isRoot = item.path === '/'
  const isActive = useMatch({ path: item.path, end: isRoot }) !== null
  const { isMobile, setOpenMobile } = useSidebar()
  const Icon = item.icon

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={isActive}>
        <NavLink to={item.path} end={isRoot} onClick={() => isMobile && setOpenMobile(false)}>
          <Icon aria-hidden="true" />
          <span>{shellMessages.nav[item.id]}</span>
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  )
}

export function AppSidebar() {
  return (
    <Sidebar>
      <SidebarHeader>
        <span className="px-2 py-1 text-base font-semibold">{shellMessages.appName}</span>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <nav aria-label={shellMessages.mainNavigation}>
              <SidebarMenu>
                {navigationItems.map((item) => (
                  <AppSidebarLink key={item.id} item={item} />
                ))}
              </SidebarMenu>
            </nav>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  )
}
