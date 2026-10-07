import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { getDashboardOverview, getSystemOverview, type SystemOverview } from '@/api/dashboard'
import { ApiError } from '@/api/errors'
import { getUnreadCount } from '@/api/notifications'
import { getAgentReport, getDashboard, getSlaReport, getTicketReport } from '@/api/reports'
import { listTasks } from '@/api/tasks'
import { getMyTickets, listTickets } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { i18n } from '@/i18n/i18n'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/dashboard', () => ({ getDashboardOverview: vi.fn(), getSystemOverview: vi.fn() }))
vi.mock('@/api/notifications', () => ({ getUnreadCount: vi.fn() }))
vi.mock('@/api/tasks', () => ({ listTasks: vi.fn() }))
vi.mock('@/api/tickets', () => ({ getMyTickets: vi.fn(), listTickets: vi.fn() }))
vi.mock('@/api/reports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/reports')>()),
  getDashboard: vi.fn(),
  getSlaReport: vi.fn(),
  getTicketReport: vi.fn(),
  getAgentReport: vi.fn(),
}))

const agentPermissions = [
  permissions.customersView,
  permissions.ticketsView,
  permissions.ticketsManage,
  permissions.notificationsView,
  permissions.tasksManage,
]
const agent: CurrentUser = { id: 'u2', email: 'agent@crm.local', fullName: 'Sara Agent', roles: ['Agent'], permissions: agentPermissions }
const supervisor: CurrentUser = {
  ...agent,
  roles: ['Supervisor'],
  permissions: [...agentPermissions, permissions.ticketsAssign, permissions.reportsView],
}
const superAdmin: CurrentUser = { ...agent, id: 'u1', fullName: 'System Administrator', roles: ['SuperAdmin'], permissions: Object.values(permissions) }

const system: SystemOverview = {
  generatedAt: '2026-10-06T12:00:00Z',
  customers: 60,
  ticketsByStatus: [
    { key: 'new', count: 11 },
    { key: 'open', count: 22 },
    { key: 'pending', count: 33 },
    { key: 'resolved', count: 44 },
    { key: 'closed', count: 55 },
  ],
  slaBreachedNow: 7,
  chats: { waiting: 2, active: 3, today: 9 },
  tasks: { open: 14, overdue: 4 },
  quickReplies: { personal: 6, shared: 8 },
  kb: { publishedArticles: 21, draftArticles: 5, publishedFaqs: 12, draftFaqs: 1 },
  portal: { surveysSent: 40, surveysAnswered: 25, accounts: 30 },
  users: {
    byRole: [
      { key: 'SuperAdmin', count: 1 },
      { key: 'Admin', count: 2 },
      { key: 'Supervisor', count: 3 },
      { key: 'Agent', count: 7 },
    ],
    active: 13,
    activeAgents: 7,
    onDutyAgents: 5,
  },
  departments: { active: 4, total: 5 },
  branches: { active: 2, total: 2 },
  webFormSubmissions: 17,
  messages: [
    { channel: 'email', sent: 100, failed: 3, received: 80 },
    { channel: 'whatsapp', sent: 50, failed: 1, received: 40 },
    { channel: 'sms', sent: 10, failed: 0, received: 0 },
  ],
  apiKeys: { active: 2, total: 3 },
  webhooks: { enabled: 1, total: 2, failedDeliveries: 6 },
  erp: { configured: false, synced: 9, failed: 2 },
  ai: { configured: false },
  recentActivity: [
    { id: 2, occurredAt: '2026-10-06T11:00:00Z', userEmail: 'admin@crm.com', action: 'user.created', entityType: 'User', entityId: 'u9' },
  ],
}

function renderDashboard() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

/** The link card with this name inside the system overview. */
async function card(name: RegExp) {
  const region = await screen.findByRole('region', { name: /System overview|نظرة عامة على النظام/ })
  return within(region).findByRole('link', { name })
}

describe('System overview on the dashboard', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockResolvedValue(superAdmin)
    vi.mocked(getSystemOverview).mockReset().mockResolvedValue(system)
    vi.mocked(getDashboardOverview).mockReset().mockRejectedValue(new ApiError('x', 500))
    vi.mocked(getMyTickets).mockReset().mockRejectedValue(new ApiError('x', 500))
    vi.mocked(listTickets).mockReset().mockRejectedValue(new ApiError('x', 500))
    vi.mocked(listTasks).mockReset().mockResolvedValue([])
    vi.mocked(getUnreadCount).mockReset().mockResolvedValue({ count: 0 })
    vi.mocked(getSlaReport).mockReset().mockRejectedValue(new ApiError('x', 500))
    vi.mocked(getDashboard).mockReset().mockRejectedValue(new ApiError('x', 500))
    vi.mocked(getTicketReport).mockReset().mockRejectedValue(new ApiError('x', 500))
    vi.mocked(getAgentReport).mockReset().mockRejectedValue(new ApiError('x', 500))
  })

  afterEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('shows every module with counts and links for a SuperAdmin', async () => {
    renderDashboard()

    const expected: [RegExp, string, string][] = [
      [/^Customers\b/, '60', '/customers'],
      [/^New\b/, '11', '/tickets?status=new'],
      [/^Closed\b/, '55', '/tickets?status=closed'],
      [/^SLA breached now\b/, '7', '/reports/sla'],
      [/^Chats waiting\b/, '2', '/chat'],
      [/^Chats active\b/, '3', '/chat'],
      [/^Open tasks\b/, '14', '/tasks'],
      [/^Overdue tasks\b/, '4', '/tasks'],
      [/^Shared quick replies\b/, '8', '/quick-replies'],
      [/^Published articles\b/, '21', '/knowledge-base'],
      [/^Draft articles\b/, '5', '/knowledge-base'],
      [/^Surveys answered\b/, '25', '/reports/satisfaction'],
      [/^Agents\b(?! on)/, '7', '/users'],
      [/^Agents on duty\b/, '5', '/assignment'],
      [/^Departments\b/, '4', '/departments'],
      [/^Branches\b/, '2', '/branches'],
      [/^Web form tickets\b/, '17', '/web-forms'],
      [/^Email sent\b/, '100', '/settings'],
      [/^API keys\b/, '2', '/integrations/api-keys'],
      [/^Webhooks\b/, '1', '/integrations/webhooks'],
      [/^Failed webhook deliveries\b/, '6', '/integrations/webhooks'],
      [/^ERP syncs\b/, '9', '/integrations/erp-logs'],
    ]
    for (const [name, value, href] of expected) {
      const link = await card(name)
      expect(within(link).getByText(value), String(name)).toBeInTheDocument()
      expect(link, String(name)).toHaveAttribute('href', href)
    }
    expect(within(await card(/^AI assistant\b/)).getByText('Not configured')).toBeInTheDocument()
  }, 30_000)

  it('shows the recent activity from the audit log with a link to it', async () => {
    renderDashboard()

    const region = await screen.findByRole('region', { name: 'Recent activity' })
    expect(await within(region).findByText(/admin@crm.com/)).toBeInTheDocument()
    expect(within(region).getByText(/user\.created/)).toBeInTheDocument()
    expect(within(region).getByRole('link', { name: 'Audit log' })).toHaveAttribute('href', '/audit-logs')
  })

  it('has no recent activity without the audit permission data', async () => {
    vi.mocked(getSystemOverview).mockResolvedValue({ ...system, recentActivity: null })
    renderDashboard()

    await card(/^Customers\b/)
    expect(screen.queryByRole('region', { name: 'Recent activity' })).not.toBeInTheDocument()
  })

  it('is not shown to a Supervisor or an Agent and is not requested', async () => {
    for (const user of [supervisor, agent]) {
      vi.mocked(getCurrentUser).mockResolvedValue(user)
      const { unmount } = renderDashboard()
      await screen.findByText(`Welcome, ${user.fullName}`)

      expect(screen.queryByRole('region', { name: 'System overview' })).not.toBeInTheDocument()
      unmount()
    }
    expect(getSystemOverview).not.toHaveBeenCalled()
  })

  it('shows an error inside the section when the overview fails', async () => {
    vi.mocked(getSystemOverview).mockRejectedValue(new ApiError('Server error', 500))
    renderDashboard()

    expect(await screen.findByText('Could not load the system overview.')).toBeInTheDocument()
  })

  it('renders in Arabic', async () => {
    await i18n.changeLanguage('ar')
    renderDashboard()

    expect(await screen.findByRole('region', { name: 'نظرة عامة على النظام' })).toBeInTheDocument()
    expect(await card(/^العملاء/)).toHaveAttribute('href', '/customers')
    expect(await screen.findByRole('region', { name: 'آخر النشاطات' })).toBeInTheDocument()
  })
})
