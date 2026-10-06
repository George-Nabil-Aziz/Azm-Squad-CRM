import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { getTicketCustomerContext } from '@/api/tickets'
import { Badge } from '@/components/ui/badge'

/**
 * Customer context beside a ticket (CRM-30): name, contacts, total ticket count and the customer's last tickets.
 * The query is keyed by the ticket and its customer, so a ticket that moves to another customer loads the new one.
 */
export function CustomerPanel({ ticketId, customerId }: { ticketId: string; customerId: string }) {
  const { t } = useTranslation()
  const context = useQuery({
    queryKey: ['tickets', 'customer-context', ticketId, customerId],
    queryFn: ({ signal }) => getTicketCustomerContext(ticketId, signal),
  })
  const data = context.data

  return (
    <aside aria-label={t('tickets.customerPanel.title')} className="flex flex-col gap-4 rounded-lg border p-4 text-sm">
      <h2 className="text-base font-semibold">{t('tickets.customerPanel.title')}</h2>
      {context.isPending ? <p className="text-muted-foreground">{t('tickets.customerPanel.loading')}</p> : null}
      {context.isError ? <p className="text-muted-foreground">{t('errors.generic')}</p> : null}
      {data ? (
        <>
          <div className="flex flex-col gap-1">
            <p className="text-base font-medium">{data.customer.name}</p>
            {data.customerDeleted ? (
              <p className="text-muted-foreground">{t('tickets.customerPanel.deleted')}</p>
            ) : (
              <Link to={`/customers/${data.customer.id}`} className="w-fit text-primary underline-offset-4 hover:underline">
                {t('tickets.customerPanel.openProfile')}
              </Link>
            )}
          </div>

          {data.customer.contacts.length === 0 ? (
            <p className="text-muted-foreground">{t('tickets.customerPanel.noContacts')}</p>
          ) : (
            <dl className="flex flex-col gap-2">
              {data.customer.contacts.map((contact) => (
                <div key={contact.id} className="flex flex-col gap-0.5">
                  <dt className="text-muted-foreground">{t(`customers.contacts.types.${contact.type}`)}</dt>
                  <dd dir="ltr" className="text-start">
                    {contact.value}
                  </dd>
                </div>
              ))}
            </dl>
          )}

          <p className="font-medium">{t('tickets.customerPanel.total', { count: data.totalTickets })}</p>

          <ul aria-label={t('tickets.customerPanel.recent')} className="flex flex-col gap-2">
            {data.recentTickets.map((ticket) => (
              <li key={ticket.id} className="flex flex-col gap-1 rounded-md border p-2">
                <div className="flex items-center justify-between gap-2">
                  <Link to={`/tickets/${ticket.id}`} dir="ltr" className="font-medium text-primary underline-offset-4 hover:underline">
                    {ticket.number}
                  </Link>
                  <Badge variant="secondary">{t(`tickets.statuses.${ticket.status}`)}</Badge>
                </div>
                <p dir="auto" className="wrap-break-word">
                  {ticket.subject}
                </p>
                {ticket.isCurrent ? <p className="text-xs text-muted-foreground">{t('tickets.customerPanel.current')}</p> : null}
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </aside>
  )
}
