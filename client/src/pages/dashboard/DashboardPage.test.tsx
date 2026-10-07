import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { getDashboardOverview, type DashboardOverview } from '@/api/dashboard'
import { ApiError } from '@/api/errors'
import { getUnreadCount } from '@/api/notifications'
import {
  getAgentReport,
  getDashboard,
  getSlaReport,
  getTicketReport,
  type AgentReport,
  type Dashboard,
  type SlaReport,
  type TicketReport,
} from '@/api/reports'
import { listTasks, type WorkTask } from '@/api/tasks'
import { getMyTickets, listTickets, type MyTickets, type Ticket } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { i18n } from '@/i18n/i18n'
import { DashboardPage } from './DashboardPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/dashboard', () => ({ getDashboardOverview: vi.fn() }))
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
  permissions.customersManage,
  permissions.ticketsView,
  permissions.ticketsManage,
  permissions.notificationsView,
  permissions.tasksManage,
  permissions.kbView,
  permissions.chatHandle,
]
const agent: CurrentUser = { id: 'u2', email: 'agent@crm.local', fullName: 'Sara Agent', roles: ['Agent'], permissions: agentPermissions }
const supervisor: CurrentUser = {
  ...agent,
  id: 'u3',
  fullName: 'Sam Supervisor',
  roles: ['Supervisor'],
  permissions: [...agentPermissions, permissions.ticketsAssign, permissions.reportsView],
}
const admin: CurrentUser = { ...agent, id: 'u1', fullName: 'System Administrator', roles: ['SuperAdmin'], permissions: Object.values(permissions) }

function ticket(id: string, number: string, subject: string, priority: Ticket['priority'], status: Ticket['status']): Ticket {
  return {
    id,
    number,
    subject,
    description: null,
    status,
    priority,
    channel: 'manual',
    customerId: 'c1',
    customerName: 'Nour Trading',
    categoryId: null,
    categoryName: null,
    assigneeId: 'u2',
    assigneeName: 'Sara Agent',
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z',
    responseDueAt: '2026-10-01T10:00:00Z',
    resolutionDueAt: '2026-10-02T08:00:00Z',
    firstResponseAt: null,
    resolvedAt: null,
    allowedStatuses: [],
    responseBreached: false,
    resolutionBreached: false,
    escalationLevel: 0,
    responseWarnedAt: null,
  }
}

const mine: MyTickets = {
  counters: { open: 2, pending: 1, breachedToday: 1 },
  tickets: {
    items: [
      ticket('t2', 'TKT-000002', 'Printer is down', 'high', 'open'),
      ticket('t1', 'TKT-000001', 'Invoice is wrong', 'low', 'pending'),
    ],
    page: 1,
    pageSize: 50,
    totalCount: 2,
  },
}

const recent = {
  items: [ticket('t9', 'TKT-000009', 'Cannot log in', 'mid', 'new'), ticket('t8', 'TKT-000008', 'Refund request', 'high', 'open')],
  page: 1,
  pageSize: 6,
  totalCount: 2,
}

const overview: DashboardOverview = {
  generatedAt: '2026-10-06T12:00:00Z',
  openTickets: 41,
  pendingTickets: 7,
  breachedNow: 5,
  resolvedToday: 12,
  newToday: 9,
  totalCustomers: 60,
  overdue: [
    { ticketId: 't5', number: 'TKT-000005', subject: 'Server is slow', priority: 'high', assigneeName: 'Omar Agent', dueAt: '2026-10-05T10:00:00Z' },
  ],
}

const slaRow = (met: number, breached: number, minutes: number) => ({ met, breached, pending: 0, compliancePercent: null, averageMinutes: minutes })
const sla: SlaReport = {
  from: '2026-09-07',
  to: '2026-10-06',
  overall: { priority: 'all', tickets: 100, response: slaRow(45, 5, 90), resolution: slaRow(30, 20, 180) },
  priorities: [],
}

const dashboard: Dashboard = {
  generatedAt: '2026-10-06T12:00:00Z',
  openTickets: 41,
  breachedToday: 3,
  averageResponseMinutes: 90,
  averageCsat: 4.5,
  csatCount: 8,
  ticketsPerDay: [],
  ticketsByChannel: [],
}

const ticketReport: TicketReport = {
  from: '2026-09-23',
  to: '2026-10-06',
  total: 14,
  byStatus: [
    { key: 'new', count: 4 },
    { key: 'open', count: 10 },
  ],
  byCategory: [],
  byChannel: [
    { key: 'email', count: 9 },
    { key: 'manual', count: 5 },
  ],
  byPriority: [],
  byDay: [
    { date: '2026-10-05', count: 6 },
    { date: '2026-10-06', count: 8 },
  ],
}

const agents: AgentReport = {
  from: '2026-09-07',
  to: '2026-10-06',
  agents: [
    { agentId: 'a1', name: 'Layla Hassan', ticketsHandled: 30, averageFirstResponseMinutes: 20, averageResolutionMinutes: 100, slaPercent: 96, averageCsat: 4.8, csatCount: 5 },
    { agentId: 'a2', name: 'Omar Agent', ticketsHandled: 50, averageFirstResponseMinutes: 30, averageResolutionMinutes: 120, slaPercent: 80, averageCsat: null, csatCount: 0 },
  ],
}

const tasks: WorkTask[] = [
  {
    id: 'k1',
    title: 'Call Nour back',
    description: null,
    dueAt: '2026-10-07T09:00:00Z',
    ticketId: 't2',
    ticketNumber: 'TKT-000002',
    isDone: false,
    completedAt: null,
    createdAt: '2026-10-01T08:00:00Z',
  },
]

function renderDashboard() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function card(name: string | RegExp) {
  return screen.findByRole('link', { name })
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockResolvedValue(agent)
    vi.mocked(getMyTickets).mockReset().mockResolvedValue(mine)
    vi.mocked(listTickets).mockReset().mockResolvedValue(recent)
    vi.mocked(getDashboardOverview).mockReset().mockResolvedValue(overview)
    vi.mocked(listTasks).mockReset().mockResolvedValue(tasks)
    vi.mocked(getUnreadCount).mockReset().mockResolvedValue({ count: 3 })
    vi.mocked(getSlaReport).mockReset().mockResolvedValue(sla)
    vi.mocked(getDashboard).mockReset().mockResolvedValue(dashboard)
    vi.mocked(getTicketReport).mockReset().mockResolvedValue(ticketReport)
    vi.mocked(getAgentReport).mockReset().mockResolvedValue(agents)
  })

  afterEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('welcomes the signed-in user and has no API status card', async () => {
    renderDashboard()

    expect(screen.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(await screen.findByText('Welcome, Sara Agent')).toBeInTheDocument()
    expect(screen.queryByText('API status')).not.toBeInTheDocument()
  })

  describe('Tabs', () => {
    it('opens on "All work" and switches to "My work"', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(admin)
      renderDashboard()

      const all = await screen.findByRole('tab', { name: 'All work' })
      const mineTab = screen.getByRole('tab', { name: 'My work' })
      expect(all).toHaveAttribute('aria-selected', 'true')
      expect(mineTab).toHaveAttribute('aria-selected', 'false')
      expect(screen.getByRole('tabpanel', { name: 'All work' })).toBeVisible()

      fireEvent.mouseDown(mineTab, { button: 0 })
      fireEvent.click(mineTab)

      await waitFor(() => expect(mineTab).toHaveAttribute('aria-selected', 'true'))
      expect(all).toHaveAttribute('aria-selected', 'false')
      expect(screen.getByRole('tabpanel', { name: 'My work' })).toBeVisible()
    })

    it('has Arabic tab labels', async () => {
      await i18n.changeLanguage('ar')
      renderDashboard()

      expect(await screen.findByRole('tab', { name: 'كل العمل' })).toBeInTheDocument()
      expect(screen.getByRole('tab', { name: 'عملي' })).toBeInTheDocument()
    })
  })

  describe('My work', () => {
    it('shows my counters, each linking to my tickets', async () => {
      renderDashboard()

      const open = await card(/^Open\b/)
      expect(await within(open).findByText("2")).toBeInTheDocument()
      expect(open).toHaveAttribute('href', '/tickets?assignee=u2')
      const pending = screen.getByRole('link', { name: /^Pending\b/ })
      expect(within(pending).getByText('1')).toBeInTheDocument()
      expect(pending).toHaveAttribute('href', '/tickets?assignee=u2&status=pending')
      const breached = screen.getByRole('link', { name: /^Breached today\b/ })
      expect(within(breached).getByText('1')).toBeInTheDocument()
      expect(breached).toHaveAttribute('href', '/tickets?assignee=u2')
    })

    it('lists my tickets in the order of the server, each linking to its details', async () => {
      renderDashboard()

      const mineRegion = await screen.findByRole('region', { name: 'My tickets' })
      const first = await within(mineRegion).findByRole('link', { name: 'TKT-000002' })
      expect(first).toHaveAttribute('href', '/tickets/t2')
      expect(within(mineRegion).getByRole('link', { name: 'TKT-000001' })).toHaveAttribute('href', '/tickets/t1')
      const rows = within(mineRegion).getAllByRole('row').slice(1)
      expect(within(rows[0]).getByText('Printer is down')).toBeInTheDocument()
      expect(within(mineRegion).getByRole('link', { name: 'View all my tickets' })).toHaveAttribute('href', '/tickets?assignee=u2')
    })

    it('shows a message when no ticket is assigned to me', async () => {
      vi.mocked(getMyTickets).mockResolvedValue({
        counters: { open: 0, pending: 0, breachedToday: 0 },
        tickets: { items: [], page: 1, pageSize: 50, totalCount: 0 },
      })
      renderDashboard()

      expect(await screen.findByText('No tickets are assigned to you.')).toBeInTheDocument()
    })

    it('shows my tasks due soon with a link to the tasks page', async () => {
      renderDashboard()

      expect(await screen.findByText('Call Nour back')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'All tasks' })).toHaveAttribute('href', '/tasks')
    })

    it('shows the unread notifications count', async () => {
      renderDashboard()

      const unread = await screen.findByRole('group', { name: 'Unread notifications' })
      expect(await within(unread).findByText('3')).toBeInTheDocument()
    })

    it('shows an error when my tickets fail to load', async () => {
      vi.mocked(getMyTickets).mockRejectedValue(new ApiError('Server error', 500))
      renderDashboard()

      expect(await screen.findByText('Could not load your tickets.')).toBeInTheDocument()
    })

    it('does not ask for tickets, tasks or notifications without their permissions', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue({ ...agent, permissions: [] })
      renderDashboard()
      await screen.findByText('Welcome, Sara Agent')

      expect(getMyTickets).not.toHaveBeenCalled()
      expect(listTasks).not.toHaveBeenCalled()
      expect(getUnreadCount).not.toHaveBeenCalled()
      expect(getDashboardOverview).not.toHaveBeenCalled()
      expect(screen.queryByRole('heading', { name: 'My tickets' })).not.toBeInTheDocument()
    })
  })

  describe('Operations overview', () => {
    it('shows the counts of all tickets and customers with links to the right filter', async () => {
      renderDashboard()

      const open = await card(/^All open\b/)
      expect(await within(open).findByText('41')).toBeInTheDocument()
      expect(open).toHaveAttribute('href', '/tickets?status=open')
      const pending = screen.getByRole('link', { name: /^All pending\b/ })
      expect(within(pending).getByText('7')).toBeInTheDocument()
      expect(pending).toHaveAttribute('href', '/tickets?status=pending')
      expect(within(screen.getByRole('link', { name: /^Breached now\b/ })).getByText('5')).toBeInTheDocument()
      const resolved = screen.getByRole('link', { name: /^Resolved today\b/ })
      expect(within(resolved).getByText('12')).toBeInTheDocument()
      expect(resolved).toHaveAttribute('href', '/tickets?status=resolved')
      const created = screen.getByRole('link', { name: /^New today\b/ })
      expect(within(created).getByText('9')).toBeInTheDocument()
      expect(created).toHaveAttribute('href', '/tickets?createdFrom=2026-10-06')
      const customers = screen.getByRole('link', { name: /^Customers\b/ })
      expect(within(customers).getByText('60')).toBeInTheDocument()
      expect(customers).toHaveAttribute('href', '/customers')
    })

    it('sends an agent (no reports.view) from "Breached now" to the tickets list', async () => {
      renderDashboard()

      expect(await card(/^Breached now\b/)).toHaveAttribute('href', '/tickets')
    })

    it('has no customers card without customers.view', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue({ ...agent, permissions: [permissions.ticketsView] })
      vi.mocked(getDashboardOverview).mockResolvedValue({ ...overview, totalCustomers: null })
      renderDashboard()

      await card(/^All open\b/)
      expect(screen.queryByRole('link', { name: /^Customers\b/ })).not.toBeInTheDocument()
    })

    it('shows an error when the overview fails', async () => {
      vi.mocked(getDashboardOverview).mockRejectedValue(new ApiError('Server error', 500))
      renderDashboard()

      expect(await screen.findByText('Could not load the operations overview.')).toBeInTheDocument()
      expect(await screen.findByText('Could not load the overdue tickets.')).toBeInTheDocument()
    })
  })

  describe('Recent and overdue tickets', () => {
    it('lists the latest tickets with status and priority badges', async () => {
      renderDashboard()

      const region = await screen.findByRole('region', { name: 'Recent tickets' })
      expect(await within(region).findByRole('link', { name: 'TKT-000009' })).toHaveAttribute('href', '/tickets/t9')
      expect(within(region).getByText('Cannot log in')).toBeInTheDocument()
      expect(within(region).getAllByText('High').length).toBeGreaterThan(0)
      expect(within(region).getByRole('link', { name: 'View all tickets' })).toHaveAttribute('href', '/tickets')
      expect(listTickets).toHaveBeenCalledWith({ page: 1, pageSize: 6 }, expect.anything())
    })

    it('lists the overdue tickets', async () => {
      renderDashboard()

      const region = await screen.findByRole('region', { name: 'Overdue tickets' })
      expect(await within(region).findByRole('link', { name: 'TKT-000005' })).toHaveAttribute('href', '/tickets/t5')
      expect(within(region).getByText('Server is slow')).toBeInTheDocument()
    })

    it('shows empty states', async () => {
      vi.mocked(getDashboardOverview).mockResolvedValue({ ...overview, overdue: [] })
      vi.mocked(listTickets).mockResolvedValue({ items: [], page: 1, pageSize: 6, totalCount: 0 })
      renderDashboard()

      expect(await screen.findByText('No overdue tickets.')).toBeInTheDocument()
      expect(await screen.findByText('No tickets yet.')).toBeInTheDocument()
    })
  })

  describe('by role', () => {
    it('an Agent sees no performance, charts or team sections and does not call the reports', async () => {
      renderDashboard()
      await card(/^All open\b/)

      expect(screen.queryByRole('region', { name: 'Performance' })).not.toBeInTheDocument()
      expect(screen.queryByRole('region', { name: 'Ticket trends' })).not.toBeInTheDocument()
      expect(screen.queryByRole('region', { name: 'Top agents' })).not.toBeInTheDocument()
      expect(getSlaReport).not.toHaveBeenCalled()
      expect(getDashboard).not.toHaveBeenCalled()
      expect(getTicketReport).not.toHaveBeenCalled()
      expect(getAgentReport).not.toHaveBeenCalled()
    })

    it('a Supervisor sees performance, charts and the team', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(supervisor)
      renderDashboard()

      const performance = await screen.findByRole('region', { name: 'Performance' })
      const compliance = await within(performance).findByRole('link', { name: /^SLA compliance\b/ })
      expect(within(compliance).getByText('75%')).toBeInTheDocument()
      expect(compliance).toHaveAttribute('href', '/reports/sla')
      expect(within(performance).getByRole('link', { name: /^Average first response\b/ })).toHaveTextContent('1 h 30 min')
      expect(within(performance).getByRole('link', { name: /^Average resolution\b/ })).toHaveTextContent('3 h')
      const csat = within(performance).getByRole('link', { name: /^Average CSAT\b/ })
      expect(within(csat).getByText('4.5')).toBeInTheDocument()
      expect(within(csat).getByText('8 ratings')).toBeInTheDocument()
      expect(csat).toHaveAttribute('href', '/reports/satisfaction')
      expect(screen.getByRole('link', { name: /^Breached now\b/ })).toHaveAttribute('href', '/reports/sla')

      const charts = screen.getByRole('region', { name: 'Ticket trends' })
      expect(await within(charts).findByRole('img', { name: 'Tickets per day' })).toBeInTheDocument()
      expect(within(charts).getByRole('img', { name: 'Tickets by channel' })).toBeInTheDocument()
      expect(within(charts).getByRole('img', { name: 'Tickets by status' })).toBeInTheDocument()
      expect(within(charts).getByRole('link', { name: 'Open ticket report' })).toHaveAttribute('href', '/reports/tickets')

      const team = screen.getByRole('region', { name: 'Top agents' })
      await within(team).findByText('Omar Agent')
      const rows = within(team).getAllByRole('row').slice(1)
      expect(within(rows[0]).getByText('Omar Agent')).toBeInTheDocument()
      expect(within(rows[0]).getByText('50')).toBeInTheDocument()
      expect(within(rows[1]).getByText('Layla Hassan')).toBeInTheDocument()
      expect(within(rows[1]).getByText('96%')).toBeInTheDocument()
      expect(within(rows[1]).getByText('4.8')).toBeInTheDocument()
      expect(within(team).getByRole('link', { name: 'Agent report' })).toHaveAttribute('href', '/reports/agents')
    })

    it('the chart range can be switched from 14 to 30 days', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(supervisor)
      renderDashboard()
      await screen.findByRole('img', { name: 'Tickets per day' })
      await waitFor(() => expect(getTicketReport).toHaveBeenCalledTimes(1))
      const [first] = vi.mocked(getTicketReport).mock.calls[0]

      fireEvent.click(screen.getByRole('button', { name: '30 days' }))

      await waitFor(() => expect(getTicketReport).toHaveBeenCalledTimes(2))
      const [second] = vi.mocked(getTicketReport).mock.calls[1]
      expect(second.from! < first.from!).toBe(true)
    })

    it('an Admin (every permission) sees every section', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(admin)
      renderDashboard()

      for (const name of ['My work', 'Operations', 'Performance', 'Ticket trends', 'Recent tickets', 'Overdue tickets', 'Top agents']) {
        expect(await screen.findByRole('region', { name })).toBeInTheDocument()
      }
    })

    it('shows a message in a section whose report fails', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(supervisor)
      vi.mocked(getAgentReport).mockRejectedValue(new ApiError('Server error', 500))
      renderDashboard()

      expect(await screen.findByText('Could not load the agents.')).toBeInTheDocument()
    })
  })

  it('renders in Arabic', async () => {
    await i18n.changeLanguage('ar')
    vi.mocked(getCurrentUser).mockResolvedValue(supervisor)
    renderDashboard()

    expect(await screen.findByRole('region', { name: 'عملي' })).toBeInTheDocument()
    expect(await screen.findByRole('region', { name: 'نظرة عامة على العمليات' })).toBeInTheDocument()
    expect(await screen.findByRole('region', { name: 'الأداء' })).toBeInTheDocument()
    expect(await screen.findByRole('region', { name: 'أفضل الموظفين' })).toBeInTheDocument()
    expect(await screen.findByRole("link", { name: /^كل التذاكر المفتوحة/ })).toHaveAttribute("href", "/tickets?status=open")
  })
})
