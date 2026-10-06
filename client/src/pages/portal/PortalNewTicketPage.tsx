import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { PortalTicket } from '@/api/portal'
import { PortalTicketForm } from '@/features/portal/PortalTicketForm'

/** Request form, then the ticket number the customer got (and a hint that a confirmation was emailed). */
export function PortalNewTicketPage() {
  const { t } = useTranslation()
  const [ticket, setTicket] = useState<PortalTicket | null>(null)

  return (
    <div className="mx-auto flex w-full max-w-xl flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('portal.newTicket.title')}</h1>
      {ticket ? (
        <div role="status" className="flex flex-col gap-3 rounded-lg border p-4">
          <p className="font-medium">{t('portal.newTicket.created')}</p>
          <p>
            {t('portal.newTicket.number')}{' '}
            <span dir="ltr" className="font-semibold">
              {ticket.number}
            </span>
          </p>
          <p className="text-sm text-muted-foreground">{t('portal.newTicket.emailSent')}</p>
          <div className="flex gap-3">
            <Link to="/portal/tickets" className="text-primary underline-offset-4 hover:underline">
              {t('portal.newTicket.viewRequests')}
            </Link>
            <button type="button" className="text-primary underline-offset-4 hover:underline" onClick={() => setTicket(null)}>
              {t('portal.newTicket.another')}
            </button>
          </div>
        </div>
      ) : (
        <PortalTicketForm onSubmitted={setTicket} />
      )}
    </div>
  )
}
