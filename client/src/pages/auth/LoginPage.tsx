import { useTranslation } from 'react-i18next'
import { Navigate, useLocation } from 'react-router'
import { getReturnPath } from '@/app/return-path'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { LoginForm } from '@/features/auth/LoginForm'

export function LoginPage() {
  const { t } = useTranslation()
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  // Already signed in, or just signed in: go to the page the user asked for (default: dashboard).
  if (isAuthenticated) return <Navigate to={getReturnPath(location.state)} replace />

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl font-semibold">{t('app.name')}</h1>
          </CardTitle>
          <CardDescription>{t('auth.signInDescription')}</CardDescription>
          <CardAction>
            <LanguageSwitcher />
          </CardAction>
        </CardHeader>
        <CardContent>
          <LoginForm />
        </CardContent>
      </Card>
    </div>
  )
}
