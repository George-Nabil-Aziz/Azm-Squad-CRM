import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { changeTicketStatus, type Ticket } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Can } from '@/features/auth/Can'
import type { TicketStatus } from './ticket-values'
import { ticketsQueryKey } from './useTickets'

/** The action label of moving a ticket to `target`: opening a resolved or closed ticket is a reopen. */
type TargetStatus = Exclude<TicketStatus, 'new'>

function actionKey(from: TicketStatus, target: TargetStatus) {
  return target === 'open' && (from === 'resolved' || from === 'closed') ? 'reopen' : target
}

/** One button per status the workflow allows next (the server sends `allowedStatuses`; it enforces the rules too). */
export function TicketStatusActions({ ticket }: { ticket: Ticket }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const change = useMutation({
    mutationFn: (status: TicketStatus) => changeTicketStatus(ticket.id, status),
    onSuccess: async (updated) => {
      toast.success(t('tickets.details.statusChanged', { status: t(`tickets.statuses.${updated.status}`) }))
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ticketsQueryKey })
    },
  })

  async function move(status: TicketStatus) {
    try {
      await change.mutateAsync(status)
    } catch (caught) {
      const message = isApiError(caught) ? caught.problem?.errors?.status?.[0] : undefined
      if (message) setError(message)
    }
  }

  if (ticket.allowedStatuses.length === 0) return null

  return (
    <Can permission={permissions.ticketsManage}>
      <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('tickets.details.statusActions.title')}>
        {ticket.allowedStatuses
          .filter((status): status is TargetStatus => status !== 'new') // nothing moves back to New
          .map((status) => (
          <Button
            key={status}
            type="button"
            size="sm"
            variant={status === 'open' ? 'outline' : 'default'}
            disabled={change.isPending}
            onClick={() => void move(status)}
          >
            {t(`tickets.details.statusActions.${actionKey(ticket.status, status)}`)}
          </Button>
        ))}
        {error ? (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        ) : null}
      </div>
    </Can>
  )
}
