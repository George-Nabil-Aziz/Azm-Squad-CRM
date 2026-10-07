import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { toneBarClasses, toneSoftClasses, type Tone } from '@/lib/tones'

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
  /** Icon in a soft coloured tile, with a matching accent line on top of the card. */
  icon?: LucideIcon
  accent?: Tone
}

/** One number of the dashboard; the whole card links to its detailed page. */
export function StatCard({ title, value, hint, to, tone = 'default', size = 'default', icon: Icon, accent = 'indigo' }: StatCardProps) {
  const color = tone === 'danger' ? 'rose' : accent
  const body = (
    <Card
      size={size === 'compact' ? 'sm' : 'default'}
      className={cn('relative h-full', to && 'transition-colors hover:bg-accent/50')}
      {...(to ? {} : { role: 'group', 'aria-label': title })}
    >
      <span aria-hidden="true" className={cn('absolute inset-x-0 top-0 h-1 opacity-80', toneBarClasses[color])} />
      <CardHeader className="flex items-center justify-between gap-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        {Icon ? (
          <span aria-hidden="true" className={cn('flex size-8 shrink-0 items-center justify-center rounded-lg', toneSoftClasses[color])}>
            <Icon className="size-4" />
          </span>
        ) : null}
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
