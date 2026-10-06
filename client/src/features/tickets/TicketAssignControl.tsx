import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { assignTicket, type Ticket } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Can } from '@/features/auth/Can'
import { useCurrentUser } from '@/features/auth/useCurrentUser'
import { ticketsQueryKey, useTicketAssignees } from './useTickets'

/**
 * Who works on the ticket: supervisors (tickets.assign) pick any agent or none; everyone else with
 * tickets.manage can only take the ticket for themselves. The server enforces the same rules.
 */
export function TicketAssignControl({ ticket }: { ticket: Ticket }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: me } = useCurrentUser()
  const assignees = useTicketAssignees()
  const [choice, setChoice] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const selected = choice ?? ticket.assigneeId ?? ''

  const assign = useMutation({
    mutationFn: (assigneeId: string | null) => assignTicket(ticket.id, assigneeId),
    onSuccess: async (updated) => {
      toast.success(t(updated.assigneeId ? 'tickets.details.assigned' : 'tickets.details.unassigned'))
      setChoice(null)
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ticketsQueryKey })
    },
  })

  async function submit(assigneeId: string | null) {
    try {
      await assign.mutateAsync(assigneeId)
    } catch (caught) {
      const message = isApiError(caught) ? caught.problem?.errors?.assigneeId?.[0] : undefined
      if (message) setError(message)
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Can permission={permissions.ticketsAssign}>
        <label htmlFor="ticket-assignee" className="text-sm text-muted-foreground">
          {t('tickets.details.assignTo')}
        </label>
        <NativeSelect id="ticket-assignee" value={selected} onChange={(event) => setChoice(event.target.value)}>
          <NativeSelectOption value="">{t('tickets.filters.unassigned')}</NativeSelectOption>
          {assignees.data?.map((assignee) => (
            <NativeSelectOption key={assignee.id} value={assignee.id}>
              {assignee.fullName}
            </NativeSelectOption>
          ))}
        </NativeSelect>
        <Button
          type="button"
          size="sm"
          disabled={assign.isPending || selected === (ticket.assigneeId ?? '')}
          onClick={() => void submit(selected === '' ? null : selected)}
        >
          {t('tickets.details.assign')}
        </Button>
      </Can>
      {me && ticket.assigneeId !== me.id && !me.permissions.includes(permissions.ticketsAssign) ? (
        <Can permission={permissions.ticketsManage}>
          <Button type="button" size="sm" variant="outline" disabled={assign.isPending} onClick={() => void submit(me.id)}>
            {t('tickets.details.assignToMe')}
          </Button>
        </Can>
      ) : null}
      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  )
}
