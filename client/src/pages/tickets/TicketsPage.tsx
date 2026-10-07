import { PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Can } from '@/features/auth/Can'
import { NewTicketDialog } from '@/features/tickets/NewTicketDialog'
import {
  emptyTicketFilters,
  TicketFilters,
  toListParams,
  type TicketFilterValues,
} from '@/features/tickets/TicketFilters'
import { TicketsTable } from '@/features/tickets/TicketsTable'
import { ticketPriorities, ticketStatuses } from '@/features/tickets/ticket-values'
import { useTickets } from '@/features/tickets/useTickets'

const PAGE_SIZE = 20

/** Filters named by the address (?status=&priority=&assignee=&createdFrom=), e.g. from the dashboard links; unknown values are ignored. */
function filtersFromAddress(params: URLSearchParams): TicketFilterValues {
  const status = params.get('status')
  const priority = params.get('priority')
  const createdFrom = params.get('createdFrom') ?? ''
  return {
    ...emptyTicketFilters,
    status: ticketStatuses.find((value) => value === status) ?? '',
    priority: ticketPriorities.find((value) => value === priority) ?? '',
    assignee: params.get('assignee') ?? '',
    createdFrom: /^\d{4}-\d{2}-\d{2}$/.test(createdFrom) ? createdFrom : '',
  }
}

/** Tickets page: search, filters, paged list (newest first) and the new-ticket dialog. */
export function TicketsPage() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()
  const [creating, setCreating] = useState(false)
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [filters, setFilters] = useState<TicketFilterValues>(() => filtersFromAddress(searchParams))
  const [page, setPage] = useState(1)
  const tickets = useTickets({
    ...toListParams(filters),
    ...(search ? { search } : {}),
    page,
    pageSize: PAGE_SIZE,
  })

  const totalPages = tickets.data ? Math.max(1, Math.ceil(tickets.data.totalCount / tickets.data.pageSize)) : 1

  function onSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchText.trim())
    setPage(1)
  }

  function onFiltersChange(next: TicketFilterValues) {
    setFilters(next)
    setPage(1)
  }

  function onClear() {
    setFilters(emptyTicketFilters)
    setSearchText('')
    setSearch('')
    setPage(1)
  }

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

      <form role="search" className="flex max-w-md gap-2" onSubmit={onSearch}>
        <Input
          type="search"
          aria-label={t('tickets.searchLabel')}
          placeholder={t('tickets.searchLabel')}
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
        />
        <Button type="submit" variant="outline">
          <SearchIcon aria-hidden="true" />
          {t('tickets.search')}
        </Button>
      </form>

      <TicketFilters value={filters} onChange={onFiltersChange} onClear={onClear} />

      {tickets.isPending ? (
        <p className="text-muted-foreground">{t('tickets.loading')}</p>
      ) : tickets.data && tickets.data.items.length > 0 ? (
        <TicketsTable tickets={tickets.data.items} />
      ) : (
        <p className="text-muted-foreground">{t('tickets.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('tickets.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('tickets.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('tickets.next')}
        </Button>
      </div>

      {creating ? <NewTicketDialog onClose={() => setCreating(false)} /> : null}
    </div>
  )
}
