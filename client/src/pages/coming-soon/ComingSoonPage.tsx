import { shellMessages, type NavigationId } from '@/app/messages'

/** Placeholder for a sidebar area whose story is not built yet (keeps every navigation link working). */
export function ComingSoonPage({ area }: { area: NavigationId }) {
  return (
    <div className="flex flex-col gap-1">
      <h1 className="text-2xl font-semibold">{shellMessages.nav[area]}</h1>
      <p className="text-muted-foreground">{shellMessages.comingSoon}</p>
    </div>
  )
}
