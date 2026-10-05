import {
  BookOpenIcon,
  ChartColumnIcon,
  LayoutDashboardIcon,
  TicketIcon,
  UserCogIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import type { NavigationId } from './messages'

export interface NavigationItem {
  /** Key of the label in shellMessages.nav (CRM-4: translation key). */
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
