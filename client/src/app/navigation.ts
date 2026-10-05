import {
  BookOpenIcon,
  ChartColumnIcon,
  LayoutDashboardIcon,
  TicketIcon,
  UserCogIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import type en from '@/i18n/en.json'

/** Key of a sidebar area: its label is the translation `nav.<id>` in src/i18n/{en,ar}.json. */
export type NavigationId = keyof (typeof en)['nav']

export interface NavigationItem {
  /** Translation key suffix: the label is t(`nav.${id}`). */
  id: NavigationId
  /** Absolute route path. Every path here has a route in AppRoutes.tsx (real page or "coming soon"). */
  path: string
  icon: LucideIcon
}

/** Sidebar items, in display order. */
export const navigationItems: readonly NavigationItem[] = [
  { id: 'dashboard', path: '/', icon: LayoutDashboardIcon },
  { id: 'tickets', path: '/tickets', icon: TicketIcon },
  { id: 'customers', path: '/customers', icon: UsersIcon },
  { id: 'knowledgeBase', path: '/knowledge-base', icon: BookOpenIcon },
  { id: 'reports', path: '/reports', icon: ChartColumnIcon },
  { id: 'users', path: '/users', icon: UserCogIcon },
]
