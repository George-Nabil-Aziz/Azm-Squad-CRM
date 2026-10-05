import { Navigate, Route, Routes } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { ComingSoonPage } from '@/pages/coming-soon/ComingSoonPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { RequireAuth } from './RequireAuth'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          {/* Areas built by later stories: each story replaces its line with the real page routes. */}
          <Route path="tickets" element={<ComingSoonPage area="tickets" />} />
          <Route path="customers" element={<ComingSoonPage area="customers" />} />
          <Route path="knowledge-base" element={<ComingSoonPage area="knowledgeBase" />} />
          <Route path="reports" element={<ComingSoonPage area="reports" />} />
          <Route path="users" element={<ComingSoonPage area="users" />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
