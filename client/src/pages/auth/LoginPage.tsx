import { Navigate, useLocation } from 'react-router'
import { shellMessages } from '@/app/messages'
import { getReturnPath } from '@/app/return-path'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { authMessages } from '@/features/auth/auth-messages'
import { LoginForm } from '@/features/auth/LoginForm'

export function LoginPage() {
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  // Already signed in, or just signed in: go to the page the user asked for (default: dashboard).
  if (isAuthenticated) return <Navigate to={getReturnPath(location.state)} replace />

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl font-semibold">{shellMessages.appName}</h1>
          </CardTitle>
          <CardDescription>{authMessages.signInDescription}</CardDescription>
        </CardHeader>
        <CardContent>
          <LoginForm />
        </CardContent>
      </Card>
    </div>
  )
}
