import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

interface StatCardProps {
  title: string
  /** undefined = still loading (skeleton). */
  value: ReactNode | undefined
  hint?: string
  /** The detailed page; without it the card is a plain group. */
  to?: string
  tone?: 'default' | 'danger'
  /** compact = a small card for the system overview. */
  size?: 'default' | 'compact'
}

/** One number of the dashboard; the whole card links to its detailed page. */
export function StatCard({ title, value, hint, to, tone = 'default', size = 'default' }: StatCardProps) {
  const body = (
    <Card size={size === 'compact' ? 'sm' : 'default'} className={cn('h-full', to && 'transition-colors hover:bg-accent/50')} {...(to ? {} : { role: 'group', 'aria-label': title })}>
      <CardHeader>
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-1">
        {value === undefined ? (
          <Skeleton className="h-9 w-16" />
        ) : (
          <p className={cn(size === 'compact' ? 'text-2xl' : 'text-3xl', 'font-semibold tabular-nums', tone === 'danger' && 'text-destructive')}>{value}</p>
        )}
        {hint ? <p className="text-sm text-muted-foreground">{hint}</p> : null}
      </CardContent>
    </Card>
  )

  return to ? (
    <Link to={to} className="block rounded-xl outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
      {body}
    </Link>
  ) : (
    body
  )
}
