import { useQuery } from '@tanstack/react-query'
import { getHealth } from '@/api/health'
import { shellMessages } from '@/app/messages'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Placeholder dashboard: welcome line + API health. Real widgets come with the reports stories. */
export function DashboardPage() {
  const { data: user } = useCurrentUser()
  const health = useQuery({ queryKey: ['health'], queryFn: ({ signal }) => getHealth(signal) })

  const apiStatus = health.isError
    ? shellMessages.apiUnavailable
    : (health.data?.status ?? shellMessages.apiLoading)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{shellMessages.nav.dashboard}</h1>
        {user ? <p className="text-muted-foreground">{shellMessages.welcome(user.fullName)}</p> : null}
      </div>
      <Card className="max-w-sm">
        <CardHeader>
          <CardTitle>{shellMessages.apiStatus}</CardTitle>
        </CardHeader>
        <CardContent>
          <p>{apiStatus}</p>
        </CardContent>
      </Card>
    </div>
  )
}
