import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Can } from '@/features/auth/Can'
import { NewTicketDialog } from '@/features/tickets/NewTicketDialog'

/** Tickets page with the new-ticket dialog (CRM-13). */
export function TicketsPage() {
  const { t } = useTranslation()
  const [creating, setCreating] = useState(false)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold">{t('nav.tickets')}</h1>
          <p className="text-muted-foreground">{t('tickets.description')}</p>
        </div>
        <Can permission={permissions.ticketsManage}>
          <Button onClick={() => setCreating(true)}>
            <PlusIcon aria-hidden="true" />
            {t('tickets.new')}
          </Button>
        </Can>
      </div>

      {creating ? <NewTicketDialog onClose={() => setCreating(false)} /> : null}
    </div>
  )
}
