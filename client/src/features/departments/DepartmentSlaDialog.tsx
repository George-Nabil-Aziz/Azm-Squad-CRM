import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import {
  removeDepartmentSlaPolicy,
  setDepartmentSlaPolicy,
  type Department,
  type DepartmentSlaPolicy,
} from '@/api/departments'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ticketPriorities, type TicketPriority } from '@/features/tickets/ticket-values'
import { departmentsQueryKey, useDepartmentSlaPolicies } from './useDepartments'

interface DepartmentSlaDialogProps {
  department: Department
  onClose: () => void
}

/** The optional SLA overrides of a department: one row per priority (empty = the global policy applies). */
export function DepartmentSlaDialog({ department, onClose }: DepartmentSlaDialogProps) {
  const { t } = useTranslation()
  const policies = useDepartmentSlaPolicies(department.id)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('departments.slaTitle', { name: department.name })}</DialogTitle>
          <DialogDescription>{t('departments.slaDescription')}</DialogDescription>
        </DialogHeader>
        {policies.isPending ? (
          <p className="text-muted-foreground">{t('departments.loading')}</p>
        ) : (
          <div className="flex flex-col gap-4">
            {ticketPriorities.map((priority) => (
              <PriorityRow
                key={priority}
                departmentId={department.id}
                priority={priority}
                current={policies.data?.find((policy) => policy.priority === priority)}
              />
            ))}
          </div>
        )}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {t('departments.close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface PriorityRowProps {
  departmentId: string
  priority: TicketPriority
  current?: DepartmentSlaPolicy
}

function PriorityRow({ departmentId, priority, current }: PriorityRowProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [response, setResponse] = useState(current ? String(current.responseMinutes) : '')
  const [resolution, setResolution] = useState(current ? String(current.resolutionMinutes) : '')
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () =>
      setDepartmentSlaPolicy(departmentId, priority, {
        responseMinutes: Number(response),
        resolutionMinutes: Number(resolution),
      }),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: departmentsQueryKey })
      toast.success(t('departments.slaSaved'))
    },
    onError: (caught) => {
      const errors = isApiError(caught) && caught.status === 400 ? caught.problem?.errors : undefined
      setError(Object.values(errors ?? {})[0]?.[0] ?? null)
    },
  })

  const remove = useMutation({
    mutationFn: () => removeDepartmentSlaPolicy(departmentId, priority),
    onSuccess: async () => {
      setResponse('')
      setResolution('')
      setError(null)
      await queryClient.invalidateQueries({ queryKey: departmentsQueryKey })
      toast.success(t('departments.slaRemoved'))
    },
  })

  const label = t(`tickets.priorities.${priority}`)
  const complete = response.trim() !== '' && resolution.trim() !== ''

  return (
    <div role="group" aria-label={label} className="flex flex-col gap-2 rounded-md border p-3">
      <div className="flex items-center justify-between gap-2">
        <span className="font-medium">{label}</span>
        <span className="text-sm text-muted-foreground">
          {t(current ? 'departments.slaOverridden' : 'departments.slaGlobal')}
        </span>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1 text-sm">
          {t('departments.slaResponse')}
          <Input
            type="number"
            min={1}
            inputMode="numeric"
            value={response}
            onChange={(event) => setResponse(event.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          {t('departments.slaResolution')}
          <Input
            type="number"
            min={1}
            inputMode="numeric"
            value={resolution}
            onChange={(event) => setResolution(event.target.value)}
          />
        </label>
      </div>
      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" disabled={!complete || save.isPending} onClick={() => save.mutate()}>
          {t('departments.slaSave')}
        </Button>
        {current ? (
          <Button type="button" size="sm" variant="outline" disabled={remove.isPending} onClick={() => remove.mutate()}>
            {t('departments.slaRemove')}
          </Button>
        ) : null}
      </div>
    </div>
  )
}
