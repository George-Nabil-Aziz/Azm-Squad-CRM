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
import { usePermissions } from '@/features/auth/usePermissions'
import { BrandLogo } from '@/features/branding/BrandLogo'

function AppSidebarLink({ item }: { item: NavigationItem }) {
  const { t, i18n } = useTranslation()
  const isRoot = item.path === '/'
  const isActive = useMatch({ path: item.path, end: isRoot }) !== null
  const { isMobile, setOpenMobile } = useSidebar()
  const label = t(`nav.${item.id}`)
  const dir = i18n.dir()
  const Icon = item.icon

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={isActive} tooltip={{ children: label, side: dir === 'rtl' ? 'left' : 'right' }}>
        <NavLink to={item.path} end={isRoot} onClick={() => isMobile && setOpenMobile(false)}>
          <Icon aria-hidden="true" />
          <span>{label}</span>
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  )
}

export function AppSidebar() {
  const { t, i18n } = useTranslation()
  const { can } = usePermissions()
  const dir = i18n.dir()
  // Items with a permission appear once the user's permissions have loaded, and only if the user has it.
  const visibleItems = navigationItems.filter((item) => !item.permission || can(item.permission))

  // The sidebar sits on the reading-start side: left in English, right in Arabic (also the mobile sheet).
  return (
    <Sidebar side={dir === 'rtl' ? 'right' : 'left'} dir={dir} collapsible="icon">
      <SidebarHeader className="border-b border-sidebar-border bg-gradient-to-b from-primary/10 to-transparent">
        <div className="flex items-center gap-2 px-2 py-1">
          <BrandLogo alt={t('app.name')} />
          <span className="text-base font-semibold group-data-[collapsible=icon]:hidden">{t('app.name')}</span>
        </div>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <nav aria-label={t('shell.mainNavigation')}>
              <SidebarMenu>
                {visibleItems.map((item) => (
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
