import { PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { User } from '@/api/users'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { UserFormDialog } from '@/features/users/UserFormDialog'
import { UsersTable } from '@/features/users/UsersTable'
import { useUsers } from '@/features/users/useUsers'

const PAGE_SIZE = 20

type DialogState = { mode: 'create' } | { mode: 'edit'; user: User } | null

/** Users admin page: search, paged table, create / edit dialog, deactivate / reactivate. */
export function UsersPage() {
  const { t } = useTranslation()
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [dialog, setDialog] = useState<DialogState>(null)
  const users = useUsers({ search: search || undefined, page, pageSize: PAGE_SIZE })

  const totalPages = users.data ? Math.max(1, Math.ceil(users.data.totalCount / users.data.pageSize)) : 1

  function onSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchText.trim())
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold text-primary">{t('nav.users')}</h1>
          <p className="text-muted-foreground">{t('users.description')}</p>
        </div>
        <Button onClick={() => setDialog({ mode: 'create' })}>
          <PlusIcon aria-hidden="true" />
          {t('users.add')}
        </Button>
      </div>

      <form role="search" className="flex max-w-md gap-2" onSubmit={onSearch}>
        <Input
          type="search"
          aria-label={t('users.searchLabel')}
          placeholder={t('users.searchLabel')}
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
        />
        <Button type="submit" variant="outline">
          <SearchIcon aria-hidden="true" />
          {t('users.search')}
        </Button>
      </form>

      {users.isPending ? (
        <p className="text-muted-foreground">{t('users.loading')}</p>
      ) : users.data && users.data.items.length > 0 ? (
        <UsersTable users={users.data.items} onEdit={(user) => setDialog({ mode: 'edit', user })} />
      ) : (
        <p className="text-muted-foreground">{t('users.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('users.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('users.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('users.next')}
        </Button>
      </div>

      {dialog ? (
        <UserFormDialog
          key={dialog.mode === 'edit' ? dialog.user.id : 'new'}
          user={dialog.mode === 'edit' ? dialog.user : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}
