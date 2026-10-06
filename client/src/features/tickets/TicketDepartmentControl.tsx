import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { transferTicketDepartment } from '@/api/departments'
import { isApiError } from '@/api/errors'
import type { Ticket } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Can } from '@/features/auth/Can'
import { useDepartments } from '@/features/departments/useDepartments'
import { ticketsQueryKey } from './useTickets'

/** Moves the ticket to another department (tickets.manage); the server records the transfer in the history. */
export function TicketDepartmentControl({ ticket }: { ticket: Ticket }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const departments = useDepartments({ activeOnly: true })
  const [error, setError] = useState<string | null>(null)

  const transfer = useMutation({
    mutationFn: (departmentId: string | null) => transferTicketDepartment(ticket.id, departmentId),
    onSuccess: async () => {
      toast.success(t('tickets.department.transferred'))
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ticketsQueryKey })
    },
    onError: (caught) => {
      const message = isApiError(caught) ? caught.problem?.errors?.departmentId?.[0] : undefined
      if (message) setError(message)
    },
  })

  const options = departments.data ?? []
  const current = ticket.departmentId ?? ''
  const currentMissing = ticket.departmentId != null && !options.some((department) => department.id === ticket.departmentId)

  return (
    <Can permission={permissions.ticketsManage}>
      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="ticket-department" className="text-sm text-muted-foreground">
          {t('tickets.department.label')}
        </label>
        <NativeSelect
          id="ticket-department"
          value={current}
          disabled={transfer.isPending}
          onChange={(event) => transfer.mutate(event.target.value || null)}
        >
          <NativeSelectOption value="">{t('tickets.department.none')}</NativeSelectOption>
          {currentMissing ? (
            <NativeSelectOption value={ticket.departmentId ?? ''}>{ticket.departmentName}</NativeSelectOption>
          ) : null}
          {options.map((department) => (
            <NativeSelectOption key={department.id} value={department.id}>
              {department.name}
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
