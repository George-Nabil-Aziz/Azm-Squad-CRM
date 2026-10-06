import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { changeTicketCategory, changeTicketPriority, type Ticket } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Can } from '@/features/auth/Can'
import { useTicketCategories } from '@/features/ticket-categories/useTicketCategories'
import { ticketPriorities, type TicketPriority } from './ticket-values'
import { ticketsQueryKey } from './useTickets'

/** One change: the value is read from the event when it happens (a controlled select resets afterwards). */
type Change = { kind: 'priority'; value: TicketPriority } | { kind: 'category'; value: string | null }

/** Priority and category of a ticket (tickets.manage); every change is recorded in the history by the server. */
export function TicketClassifyControl({ ticket }: { ticket: Ticket }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const categories = useTicketCategories({ activeOnly: true })
  const [error, setError] = useState<string | null>(null)

  const change = useMutation({
    mutationFn: (change: Change) => (change.kind === 'priority' ? changeTicketPriority(ticket.id, change.value) : changeTicketCategory(ticket.id, change.value)),
    onSuccess: async () => {
      toast.success(t('tickets.details.updated'))
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ticketsQueryKey })
    },
  })

  async function run(update: Change) {
    try {
      await change.mutateAsync(update)
    } catch (caught) {
      const errors = isApiError(caught) ? caught.problem?.errors : undefined
      const message = errors?.categoryId?.[0] ?? errors?.priority?.[0]
      if (message) setError(message)
    }
  }

  const options = categories.data ?? []
  const currentMissing = ticket.categoryId !== null && !options.some((category) => category.id === ticket.categoryId)

  return (
    <Can permission={permissions.ticketsManage}>
      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="ticket-priority" className="text-sm text-muted-foreground">
          {t('tickets.priority')}
        </label>
        <NativeSelect
          id="ticket-priority"
          value={ticket.priority}
          disabled={change.isPending}
          onChange={(event) => void run({ kind: 'priority', value: event.target.value as TicketPriority })}
        >
          {ticketPriorities.map((priority) => (
            <NativeSelectOption key={priority} value={priority}>
              {t(`tickets.priorities.${priority}`)}
            </NativeSelectOption>
          ))}
        </NativeSelect>
        <label htmlFor="ticket-category" className="text-sm text-muted-foreground">
          {t('tickets.category')}
        </label>
        <NativeSelect
          id="ticket-category"
          value={ticket.categoryId ?? ''}
          disabled={change.isPending}
          onChange={(event) => void run({ kind: 'category', value: event.target.value || null })}
        >
          <NativeSelectOption value="">{t('tickets.noCategory')}</NativeSelectOption>
          {currentMissing ? (
            <NativeSelectOption value={ticket.categoryId ?? ''}>{ticket.categoryName}</NativeSelectOption>
          ) : null}
          {options.map((category) => (
            <NativeSelectOption key={category.id} value={category.id}>
              {category.name}
            </NativeSelectOption>
          ))}
        </NativeSelect>
        {error ? (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        ) : null}
      </div>
    </Can>
  )
}
