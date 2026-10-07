import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { listPortalTickets } from '@/api/portal'
import { ToneBadge } from "@/components/ui/tone-badge"
import { statusTones } from "@/features/tickets/ticket-tones"
import { Button } from '@/components/ui/button'

const PAGE_SIZE = 20

/** The signed-in customer's own requests, newest first, with their status. */
export function PortalTicketsPage() {
  const { t, i18n } = useTranslation()
  const [page, setPage] = useState(1)
  const tickets = useQuery({ queryKey: ['portal', 'tickets', page], queryFn: ({ signal }) => listPortalTickets(page, signal) })
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })
  const totalPages = Math.max(1, Math.ceil((tickets.data?.totalCount ?? 0) / PAGE_SIZE))

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold text-primary">{t('portal.tickets.title')}</h1>
        <Button asChild>
          <Link to="/portal/tickets/new">{t('portal.nav.newTicket')}</Link>
        </Button>
      </div>
      {tickets.isPending ? (
        <p className="text-muted-foreground">{t('portal.loading')}</p>
      ) : tickets.data && tickets.data.items.length > 0 ? (
        <>
          <ul className="flex flex-col gap-2">
            {tickets.data.items.map((ticket) => (
              <li key={ticket.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3">
                <div className="flex flex-col">
                  <Link to={`/portal/tickets/${ticket.id}`} className="font-medium text-primary underline-offset-4 hover:underline">
                    {ticket.subject}
                  </Link>
                  <span className="text-sm text-muted-foreground">
                    <span dir="ltr">{ticket.number}</span> · {formatTime.format(new Date(ticket.updatedAt))}
                  </span>
                </div>
                <ToneBadge tone={statusTones[ticket.status]}>{t(`portal.statuses.${ticket.status}`)}</ToneBadge>
              </li>
            ))}
          </ul>
          {totalPages > 1 ? (
            <div className="flex items-center justify-between gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                {t('portal.previous')}
              </Button>
              <span className="text-sm text-muted-foreground">{t('portal.pageOf', { page, total: totalPages })}</span>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                {t('portal.next')}
              </Button>
            </div>
          ) : null}
        </>
      ) : (
        <p className="text-muted-foreground">{t('portal.tickets.empty')}</p>
      )}
    </div>
  )
}
