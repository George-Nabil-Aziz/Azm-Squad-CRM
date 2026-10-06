import { Navigate, Route, Routes } from 'react-router'
import { permissions } from '@/auth/permissions'
import { AppLayout } from '@/components/layout/AppLayout'
import { PortalLayout } from '@/components/portal/PortalLayout'
import { PortalArticlePage } from '@/pages/portal/PortalArticlePage'
import { PortalHomePage } from '@/pages/portal/PortalHomePage'
import { PortalLoginPage } from '@/pages/portal/PortalLoginPage'
import { PortalNewTicketPage } from '@/pages/portal/PortalNewTicketPage'
import { PortalSurveyPage } from '@/pages/portal/PortalSurveyPage'
import { PortalTicketDetailsPage } from '@/pages/portal/PortalTicketDetailsPage'
import { PortalTicketsPage } from '@/pages/portal/PortalTicketsPage'
import { ReportsLayout } from '@/features/reports/ReportsLayout'
import { AuditLogsPage } from '@/pages/audit/AuditLogsPage'
import { LoginPage } from '@/pages/auth/LoginPage'
import { CustomerDetailsPage } from '@/pages/customers/CustomerDetailsPage'
import { CustomersPage } from '@/pages/customers/CustomersPage'
import { AssignmentSettingsPage } from '@/pages/assignment/AssignmentSettingsPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { KnowledgeBasePage } from '@/pages/knowledge-base/KnowledgeBasePage'
import { AgentReportPage } from '@/pages/reports/AgentReportPage'
import { DashboardReportPage } from '@/pages/reports/DashboardReportPage'
import { CsatReportPage } from '@/pages/reports/CsatReportPage'
import { SlaReportPage } from '@/pages/reports/SlaReportPage'
import { TicketReportPage } from '@/pages/reports/TicketReportPage'
import { SettingsPage } from '@/pages/settings/SettingsPage'
import { SlaPoliciesPage } from '@/pages/sla/SlaPoliciesPage'
import { QuickRepliesPage } from '@/pages/quick-replies/QuickRepliesPage'
import { TasksPage } from '@/pages/tasks/TasksPage'
import { TicketCategoriesPage } from '@/pages/ticket-categories/TicketCategoriesPage'
import { TicketDetailsPage } from '@/pages/tickets/TicketDetailsPage'
import { TicketsPage } from '@/pages/tickets/TicketsPage'
import { UsersPage } from '@/pages/users/UsersPage'
import { RequireAuth } from './RequireAuth'
import { RequirePermission } from './RequirePermission'
import { RequirePortalAuth } from './RequirePortalAuth'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/portal" element={<PortalLayout />}>
        <Route index element={<PortalHomePage />} />
        <Route path="login" element={<PortalLoginPage />} />
        <Route path="kb/articles/:id" element={<PortalArticlePage />} />
        <Route path="survey/:token" element={<PortalSurveyPage />} />
        <Route element={<RequirePortalAuth />}>
          <Route path="tickets" element={<PortalTicketsPage />} />
          <Route path="tickets/new" element={<PortalNewTicketPage />} />
          <Route path="tickets/:id" element={<PortalTicketDetailsPage />} />
        </Route>
      </Route>
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          {/* Same permission as the area's item in navigation.ts. */}
          <Route element={<RequirePermission permission={permissions.usersManage} />}>
            <Route path="users" element={<UsersPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.ticketsAssign} />}>
            <Route path="assignment" element={<AssignmentSettingsPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.categoriesManage} />}>
            <Route path="ticket-categories" element={<TicketCategoriesPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.slaManage} />}>
            <Route path="sla-policies" element={<SlaPoliciesPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.auditView} />}>
            <Route path="audit-logs" element={<AuditLogsPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.settingsManage} />}>
            <Route path="settings" element={<SettingsPage />} />
          </Route>
          {/* Areas built by later stories: each story replaces its line with the real page routes. */}
          <Route element={<RequirePermission permission={permissions.ticketsView} />}>
            <Route path="tickets" element={<TicketsPage />} />
            <Route path="tickets/:id" element={<TicketDetailsPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.customersView} />}>
            <Route path="customers" element={<CustomersPage />} />
            <Route path="customers/:id" element={<CustomerDetailsPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.kbView} />}>
            <Route path="knowledge-base" element={<KnowledgeBasePage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.tasksManage} />}>
            <Route path="tasks" element={<TasksPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.ticketsManage} />}>
            <Route path="quick-replies" element={<QuickRepliesPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.reportsView} />}>
            <Route path="reports" element={<ReportsLayout />}>
              <Route index element={<Navigate to="dashboard" replace />} />
              <Route path="dashboard" element={<DashboardReportPage />} />
              <Route path="tickets" element={<TicketReportPage />} />
              <Route path="sla" element={<SlaReportPage />} />
              <Route path="agents" element={<AgentReportPage />} />
              <Route path="satisfaction" element={<CsatReportPage />} />
            </Route>
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
