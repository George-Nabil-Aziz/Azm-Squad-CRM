import { Navigate, Route, Routes } from 'react-router'
import { permissions } from '@/auth/permissions'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { ComingSoonPage } from '@/pages/coming-soon/ComingSoonPage'
import { CustomerDetailsPage } from '@/pages/customers/CustomerDetailsPage'
import { CustomersPage } from '@/pages/customers/CustomersPage'
import { AssignmentSettingsPage } from '@/pages/assignment/AssignmentSettingsPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { SlaPoliciesPage } from '@/pages/sla/SlaPoliciesPage'
import { QuickRepliesPage } from '@/pages/quick-replies/QuickRepliesPage'
import { TasksPage } from '@/pages/tasks/TasksPage'
import { TicketCategoriesPage } from '@/pages/ticket-categories/TicketCategoriesPage'
import { TicketDetailsPage } from '@/pages/tickets/TicketDetailsPage'
import { TicketsPage } from '@/pages/tickets/TicketsPage'
import { UsersPage } from '@/pages/users/UsersPage'
import { RequireAuth } from './RequireAuth'
import { RequirePermission } from './RequirePermission'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
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
          {/* Areas built by later stories: each story replaces its line with the real page routes. */}
          <Route element={<RequirePermission permission={permissions.ticketsView} />}>
            <Route path="tickets" element={<TicketsPage />} />
            <Route path="tickets/:id" element={<TicketDetailsPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.customersView} />}>
            <Route path="customers" element={<CustomersPage />} />
            <Route path="customers/:id" element={<CustomerDetailsPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.tasksManage} />}>
            <Route path="tasks" element={<TasksPage />} />
          </Route>
          <Route element={<RequirePermission permission={permissions.ticketsManage} />}>
            <Route path="quick-replies" element={<QuickRepliesPage />} />
          </Route>
          <Route path="knowledge-base" element={<ComingSoonPage area="knowledgeBase" />} />
          <Route element={<RequirePermission permission={permissions.reportsView} />}>
            <Route path="reports" element={<ComingSoonPage area="reports" />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
