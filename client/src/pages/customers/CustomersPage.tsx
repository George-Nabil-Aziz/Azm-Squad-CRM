import { PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Can } from '@/features/auth/Can'
import { CustomerContactsDialog } from '@/features/customers/CustomerContactsDialog'
import { CustomerFormDialog } from '@/features/customers/CustomerFormDialog'
import { CustomersTable } from '@/features/customers/CustomersTable'
import { useCustomers } from '@/features/customers/useCustomers'

const PAGE_SIZE = 20

type DialogState =
  | { mode: 'create' }
  | { mode: 'edit'; customer: Customer }
  | { mode: 'contacts'; customer: Customer }
  | null

/** Customers page: search (name, phone, email), paged table, create / edit dialog, contacts dialog, delete with confirmation. */
export function CustomersPage() {
  const { t } = useTranslation()
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [dialog, setDialog] = useState<DialogState>(null)
  const customers = useCustomers({ search: search || undefined, page, pageSize: PAGE_SIZE })

  const totalPages = customers.data ? Math.max(1, Math.ceil(customers.data.totalCount / customers.data.pageSize)) : 1

  function onSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchText.trim())
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold">{t('nav.customers')}</h1>
          <p className="text-muted-foreground">{t('customers.description')}</p>
        </div>
        <Can permission={permissions.customersManage}>
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('customers.add')}
          </Button>
        </Can>
      </div>

      <form role="search" className="flex max-w-md gap-2" onSubmit={onSearch}>
        <Input
          type="search"
          aria-label={t('customers.searchLabel')}
          placeholder={t('customers.searchLabel')}
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
        />
        <Button type="submit" variant="outline">
          <SearchIcon aria-hidden="true" />
          {t('customers.search')}
        </Button>
      </form>

      {customers.isPending ? (
        <p className="text-muted-foreground">{t('customers.loading')}</p>
      ) : customers.data && customers.data.items.length > 0 ? (
        <CustomersTable
          customers={customers.data.items}
          onEdit={(customer) => setDialog({ mode: 'edit', customer })}
          onContacts={(customer) => setDialog({ mode: 'contacts', customer })}
        />
      ) : (
        <p className="text-muted-foreground">{t('customers.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('customers.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('customers.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('customers.next')}
        </Button>
      </div>

      {dialog?.mode === 'contacts' ? (
        <CustomerContactsDialog key={dialog.customer.id} customer={dialog.customer} onClose={() => setDialog(null)} />
      ) : dialog ? (
        <CustomerFormDialog
          key={dialog.mode === 'edit' ? dialog.customer.id : 'new'}
          customer={dialog.mode === 'edit' ? dialog.customer : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}
