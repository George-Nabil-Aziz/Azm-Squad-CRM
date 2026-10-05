import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getHealth } from '@/api/health'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Placeholder dashboard: welcome line + API health. Real widgets come with the reports stories. */
export function DashboardPage() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()
  const health = useQuery({ queryKey: ['health'], queryFn: ({ signal }) => getHealth(signal) })

  const apiStatus = health.isError
    ? t('dashboard.apiUnavailable')
    : health.isSuccess
      ? t('dashboard.apiOk')
      : t('dashboard.apiLoading')

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.dashboard')}</h1>
        {user ? <p className="text-muted-foreground">{t('dashboard.welcome', { name: user.fullName })}</p> : null}
      </div>
      <Card className="max-w-sm">
        <CardHeader>
          <CardTitle>{t('dashboard.apiStatus')}</CardTitle>
        </CardHeader>
        <CardContent>
          <p>{apiStatus}</p>
        </CardContent>
      </Card>
    </div>
  )
}
