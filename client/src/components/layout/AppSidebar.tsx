import { useTranslation } from 'react-i18next'
import { NavLink, useMatch } from 'react-router'
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
  const { t } = useTranslation()
  const isRoot = item.path === '/'
  const isActive = useMatch({ path: item.path, end: isRoot }) !== null
  const { isMobile, setOpenMobile } = useSidebar()
  const Icon = item.icon

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={isActive}>
        <NavLink to={item.path} end={isRoot} onClick={() => isMobile && setOpenMobile(false)}>
          <Icon aria-hidden="true" />
          <span>{t(`nav.${item.id}`)}</span>
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  )
}

export function AppSidebar() {
  const { t, i18n } = useTranslation()
  const dir = i18n.dir()

  // The sidebar sits on the reading-start side: left in English, right in Arabic (also the mobile sheet).
  return (
    <Sidebar side={dir === 'rtl' ? 'right' : 'left'} dir={dir}>
      <SidebarHeader>
        <span className="px-2 py-1 text-base font-semibold">{t('app.name')}</span>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <nav aria-label={t('shell.mainNavigation')}>
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
