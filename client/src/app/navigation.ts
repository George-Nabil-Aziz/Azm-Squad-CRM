import {
  BookOpenIcon,
  ChartColumnIcon,
  Building2Icon,
  MapPinIcon,
  PaletteIcon,
  LayoutDashboardIcon,
  ScrollTextIcon,
  SettingsIcon,
  ShuffleIcon,
  ListChecksIcon,
  MessageSquareTextIcon,
  TagsIcon,
  TicketIcon,
  TimerIcon,
  UserCogIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import { permissions, type Permission } from '@/auth/permissions'
import type en from '@/i18n/en.json'

/** Key of a sidebar area: its label is the translation `nav.<id>` in src/i18n/{en,ar}.json. */
export type NavigationId = keyof (typeof en)['nav']

export interface NavigationItem {
  /** Translation key suffix: the label is t(`nav.${id}`). */
  id: NavigationId
  /** Absolute route path. Every path here has a route in AppRoutes.tsx (real page or "coming soon"). */
  path: string
  icon: LucideIcon
  /** Shown only to users with this permission (its route is wrapped in RequirePermission with the same one). */
  permission?: Permission
}

/** Sidebar items, in display order. */
export const navigationItems: readonly NavigationItem[] = [
  { id: 'dashboard', path: '/', icon: LayoutDashboardIcon },
  { id: 'tickets', path: '/tickets', icon: TicketIcon, permission: permissions.ticketsView },
  { id: 'customers', path: '/customers', icon: UsersIcon, permission: permissions.customersView },
  { id: 'tasks', path: '/tasks', icon: ListChecksIcon, permission: permissions.tasksManage },
  { id: 'quickReplies', path: '/quick-replies', icon: MessageSquareTextIcon, permission: permissions.ticketsManage },
  { id: 'knowledgeBase', path: '/knowledge-base', icon: BookOpenIcon, permission: permissions.kbView },
  { id: 'reports', path: '/reports', icon: ChartColumnIcon, permission: permissions.reportsView },
  { id: 'users', path: '/users', icon: UserCogIcon, permission: permissions.usersManage },
  { id: 'assignment', path: '/assignment', icon: ShuffleIcon, permission: permissions.ticketsAssign },
  { id: 'ticketCategories', path: '/ticket-categories', icon: TagsIcon, permission: permissions.categoriesManage },
  { id: 'departments', path: '/departments', icon: Building2Icon, permission: permissions.departmentsManage },
  { id: 'branches', path: '/branches', icon: MapPinIcon, permission: permissions.branchesManage },
  { id: 'slaPolicies', path: '/sla-policies', icon: TimerIcon, permission: permissions.slaManage },
  { id: 'auditLogs', path: '/audit-logs', icon: ScrollTextIcon, permission: permissions.auditView },
  { id: 'branding', path: '/branding', icon: PaletteIcon, permission: permissions.settingsManage },
  { id: 'settings', path: '/settings', icon: SettingsIcon, permission: permissions.settingsManage },
]
