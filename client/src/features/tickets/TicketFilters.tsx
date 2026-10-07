import { useTranslation } from 'react-i18next'
import type { TicketListParams } from '@/api/tickets'
import { Button } from '@/components/ui/button'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { useTicketCategories } from '@/features/ticket-categories/useTicketCategories'
import { ticketPriorities, ticketStatuses, type TicketPriority, type TicketStatus } from './ticket-values'
import { useTicketAssignees } from './useTickets'

/** Value of the assignee select for "tickets nobody works on yet". */
export const UNASSIGNED = 'unassigned'

/** The filter bar's values; '' = no filter. */
export interface TicketFilterValues {
  status: TicketStatus | ''
  priority: TicketPriority | ''
  categoryId: string
  /** '' (all), UNASSIGNED or a user id. */
  assignee: string
  createdFrom: string
  createdTo: string
}

export const emptyTicketFilters: TicketFilterValues = {
  status: '',
  priority: '',
  categoryId: '',
  assignee: '',
  createdFrom: '',
  createdTo: '',
}

/** Only the filters that are set, as list parameters. */
export function toListParams(filters: TicketFilterValues): TicketListParams {
  const params: TicketListParams = {}
  if (filters.status) params.status = filters.status
  if (filters.priority) params.priority = filters.priority
  if (filters.categoryId) params.categoryId = filters.categoryId
  if (filters.assignee === UNASSIGNED) params.unassigned = true
  else if (filters.assignee) params.assigneeId = filters.assignee
  if (filters.createdFrom) params.createdFrom = filters.createdFrom
  if (filters.createdTo) params.createdTo = filters.createdTo
  return params
}

interface TicketFiltersProps {
  value: TicketFilterValues
  onChange: (value: TicketFilterValues) => void
  onClear: () => void
}

/** Status, priority, category (inactive ones too), assignee (or unassigned) and created date range. */
export function TicketFilters({ value, onChange, onClear }: TicketFiltersProps) {
  const { t } = useTranslation()
  const categories = useTicketCategories({})
  const assignees = useTicketAssignees()

  function set<K extends keyof TicketFilterValues>(key: K, next: TicketFilterValues[K]) {
    onChange({ ...value, [key]: next })
  }

  return (
    <div className="grid grid-cols-1 items-end gap-3 *:min-w-0 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
      <Field>
        <FieldLabel htmlFor="filter-status">{t('tickets.filters.status')}</FieldLabel>
        <NativeSelect
          id="filter-status"
          className="w-full"
          value={value.status}
          onChange={(event) => set('status', event.target.value as TicketFilterValues['status'])}
        >
          <NativeSelectOption value="">{t('tickets.filters.all')}</NativeSelectOption>
          {ticketStatuses.map((status) => (
            <NativeSelectOption key={status} value={status}>
              {t(`tickets.statuses.${status}`)}
            </NativeSelectOption>
          ))}
        </NativeSelect>
      </Field>
      <Field>
        <FieldLabel htmlFor="filter-priority">{t('tickets.filters.priority')}</FieldLabel>
        <NativeSelect
          id="filter-priority"
          className="w-full"
          value={value.priority}
          onChange={(event) => set('priority', event.target.value as TicketFilterValues['priority'])}
        >
          <NativeSelectOption value="">{t('tickets.filters.all')}</NativeSelectOption>
          {ticketPriorities.map((priority) => (
            <NativeSelectOption key={priority} value={priority}>
              {t(`tickets.priorities.${priority}`)}
            </NativeSelectOption>
          ))}
        </NativeSelect>
      </Field>
      <Field>
        <FieldLabel htmlFor="filter-category">{t('tickets.filters.category')}</FieldLabel>
        <NativeSelect
          id="filter-category"
          className="w-full"
          value={value.categoryId}
          onChange={(event) => set('categoryId', event.target.value)}
        >
          <NativeSelectOption value="">{t('tickets.filters.all')}</NativeSelectOption>
          {categories.data?.map((category) => (
            <NativeSelectOption key={category.id} value={category.id}>
              {category.name}
            </NativeSelectOption>
          ))}
        </NativeSelect>
      </Field>
      <Field>
        <FieldLabel htmlFor="filter-assignee">{t('tickets.filters.assignee')}</FieldLabel>
        <NativeSelect
          id="filter-assignee"
          className="w-full"
          value={value.assignee}
          onChange={(event) => set('assignee', event.target.value)}
        >
          <NativeSelectOption value="">{t('tickets.filters.all')}</NativeSelectOption>
          <NativeSelectOption value={UNASSIGNED}>{t('tickets.filters.unassigned')}</NativeSelectOption>
          {assignees.data?.map((assignee) => (
            <NativeSelectOption key={assignee.id} value={assignee.id}>
              {assignee.fullName}
            </NativeSelectOption>
          ))}
        </NativeSelect>
      </Field>
      <Field>
        <FieldLabel htmlFor="filter-created-from">{t('tickets.filters.createdFrom')}</FieldLabel>
        <Input
          id="filter-created-from"
          type="date"
          value={value.createdFrom}
          max={value.createdTo || undefined}
          onChange={(event) => set('createdFrom', event.target.value)}
        />
      </Field>
      <Field>
        <FieldLabel htmlFor="filter-created-to">{t('tickets.filters.createdTo')}</FieldLabel>
        <Input
          id="filter-created-to"
          type="date"
          value={value.createdTo}
          min={value.createdFrom || undefined}
          onChange={(event) => set('createdTo', event.target.value)}
        />
      </Field>
      <Button type="button" variant="outline" onClick={onClear}>
        {t('tickets.filters.clear')}
      </Button>
    </div>
  )
}
