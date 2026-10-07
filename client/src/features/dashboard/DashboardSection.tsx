import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

interface DashboardSectionProps {
  /** Unique id of the heading (labels the region). */
  id: string
  title: string
  /** Optional link to the detailed page. */
  action?: { label: string; to: string }
  children: ReactNode
  /** Level of the heading: 2 for a top-level section, 3 for one inside another. */
  level?: 2 | 3
}

/** A titled region of the dashboard with an optional "see more" link. */
export function DashboardSection({ id, title, action, children, level = 2 }: DashboardSectionProps) {
  const Heading = level === 2 ? 'h2' : 'h3'
  return (
    <section className="flex flex-col gap-4" aria-labelledby={id}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Heading id={id} className={level === 2 ? 'text-xl font-semibold' : 'text-lg font-semibold'}>
          {title}
        </Heading>
        {action ? (
          <Button asChild variant="link" size="sm">
            <Link to={action.to}>{action.label}</Link>
          </Button>
        ) : null}
      </div>
      {children}
    </section>
  )
}

/** Placeholder rows while a section loads. */
export function SectionSkeleton({ rows = 3, label }: { rows?: number; label: string }) {
  return (
    <div className="flex flex-col gap-2" role="status" aria-label={label}>
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} className="h-8 w-full" />
      ))}
    </div>
  )
}

/** A failed section: the message stays inside the section, the rest of the page keeps working. */
export function SectionError({ message }: { message: string }) {
  return (
    <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
      {message}
    </p>
  )
}
