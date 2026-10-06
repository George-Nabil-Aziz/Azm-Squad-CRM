import { useTranslation } from 'react-i18next'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { getPortalReturnPath } from '@/app/portal-paths'
import { useIsPortalAuthenticated } from '@/auth/usePortalSession'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { PortalLoginForm } from '@/features/portal/PortalLoginForm'

/** Customer sign-in: email, then the one-time code that was mailed to it. */
export function PortalLoginPage() {
  const { t } = useTranslation()
  const isAuthenticated = useIsPortalAuthenticated()
  const location = useLocation()
  const navigate = useNavigate()
  const returnPath = getPortalReturnPath(location.state)

  if (isAuthenticated) return <Navigate to={returnPath} replace />

  return (
    <div className="mx-auto w-full max-w-sm">
      <Card>
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl font-semibold">{t('portal.login.title')}</h1>
          </CardTitle>
          <CardDescription>{t('portal.login.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <PortalLoginForm onSignedIn={() => navigate(returnPath, { replace: true })} />
        </CardContent>
      </Card>
    </div>
  )
}
