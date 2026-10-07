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
  const { can } = usePermissions()
  const dir = i18n.dir()
  // Items with a permission appear once the user's permissions have loaded, and only if the user has it.
  const visibleItems = navigationItems.filter((item) => !item.permission || can(item.permission))

  // The sidebar sits on the reading-start side: left in English, right in Arabic (also the mobile sheet).
  return (
    <Sidebar side={dir === 'rtl' ? 'right' : 'left'} dir={dir}>
      <SidebarHeader>
        <div className="flex items-center gap-2 px-2 py-1">
          <BrandLogo alt={t('app.name')} />
          <span className="text-base font-semibold">{t('app.name')}</span>
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
