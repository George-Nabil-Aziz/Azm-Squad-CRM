import { useTranslation } from 'react-i18next'
import { Link, Navigate, useLocation } from 'react-router'
import { getReturnPath } from '@/app/return-path'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { ThemeToggle } from '@/features/theme/ThemeToggle'
import { Card, CardAction, CardContent, CardDescription, CardHeader } from '@/components/ui/card'
import { LoginForm } from '@/features/auth/LoginForm'
import { BrandLogo } from '@/features/branding/BrandLogo'

/**
 * Sign-in page. Desktop: split layout, the brand panel at the start side and the sign-in card at the end side.
 * Mobile: the brand block sits above one centred card. The logo and product name link back to the landing page.
 */
export function LoginPage() {
  const { t } = useTranslation()
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  // Already signed in, or just signed in: go to the page the user asked for (default: dashboard).
  if (isAuthenticated) return <Navigate to={getReturnPath(location.state)} replace />

  return (
    <div className="grid min-h-svh bg-muted lg:grid-cols-2 lg:bg-background">
      <aside className="flex flex-col items-center justify-center gap-3 px-6 pt-8 text-center lg:items-start lg:gap-4 lg:bg-gradient-to-br lg:from-primary lg:via-primary lg:to-accent-2 lg:p-12 lg:text-start lg:text-primary-foreground">
        <h1 className="text-xl font-semibold lg:text-3xl">
          <Link
            to="/"
            aria-label={t('app.name')}
            title={t('auth.backToHome')}
            className="flex items-center gap-3 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring lg:focus-visible:ring-primary-foreground"
          >
            <BrandLogo alt="" className="size-10 lg:size-14 lg:text-primary-foreground" />
            <span>{t('app.name')}</span>
          </Link>
        </h1>
        <p className="max-w-sm text-sm text-muted-foreground lg:text-base lg:text-primary-foreground/80">{t('auth.tagline')}</p>
      </aside>
      <main className="flex items-center justify-center p-6">
        <Card className="w-full max-w-sm">
          <CardHeader>
            <CardDescription>{t('auth.signInDescription')}</CardDescription>
            <CardAction className="flex items-center gap-1">
              <LanguageSwitcher />
              <ThemeToggle />
            </CardAction>
          </CardHeader>
          <CardContent>
            <LoginForm />
          </CardContent>
        </Card>
      </main>
    </div>
  )
}
