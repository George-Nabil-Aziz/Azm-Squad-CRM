import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { auditActionKeys, auditActions, type AuditAction } from '@/api/audit-logs'
import { Button } from '@/components/ui/button'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { AuditLogsTable } from '@/features/audit/AuditLogsTable'
import { useAuditLogs } from '@/features/audit/useAuditLogs'
import { useUsers } from '@/features/users/useUsers'

const PAGE_SIZE = 20

/** Read-only audit log: filter by user, action and date range (UTC days), paged, newest first. */
export function AuditLogsPage() {
  const { t } = useTranslation()
  const [userId, setUserId] = useState('')
  const [action, setAction] = useState<AuditAction | ''>('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [page, setPage] = useState(1)
  const users = useUsers({ pageSize: 100 })
  const logs = useAuditLogs({
    userId: userId || undefined,
    action: action || undefined,
    from: fromDate ? `${fromDate}T00:00:00.000Z` : undefined,
    to: toDate ? `${toDate}T23:59:59.999Z` : undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const totalPages = logs.data ? Math.max(1, Math.ceil(logs.data.totalCount / logs.data.pageSize)) : 1

  /** A filter changed: show the first page of the new result. */
  function onFilter<T>(setter: (value: T) => void, value: T) {
    setter(value)
    setPage(1)
  }

  function clear() {
    setUserId('')
    setAction('')
    setFromDate('')
    setToDate('')
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.auditLogs')}</h1>
        <p className="text-muted-foreground">{t('auditLogs.description')}</p>
      </div>

      <div className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <Field>
          <FieldLabel htmlFor="audit-user">{t('auditLogs.filters.user')}</FieldLabel>
          <NativeSelect id="audit-user" className="w-full" value={userId} onChange={(event) => onFilter(setUserId, event.target.value)}>
            <NativeSelectOption value="">{t('auditLogs.filters.all')}</NativeSelectOption>
            {users.data?.items.map((user) => (
              <NativeSelectOption key={user.id} value={user.id}>
                {user.fullName} ({user.email})
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
        <Field>
          <FieldLabel htmlFor="audit-action">{t('auditLogs.filters.action')}</FieldLabel>
          <NativeSelect
            id="audit-action"
            className="w-full"
            value={action}
            onChange={(event) => onFilter(setAction, event.target.value as AuditAction | '')}
          >
            <NativeSelectOption value="">{t('auditLogs.filters.all')}</NativeSelectOption>
            {auditActions.map((value) => (
              <NativeSelectOption key={value} value={value}>
                {t(`auditLogs.actions.${auditActionKeys[value]}`)}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
        <Field>
          <FieldLabel htmlFor="audit-from">{t('auditLogs.filters.from')}</FieldLabel>
          <Input
            id="audit-from"
            type="date"
            value={fromDate}
            max={toDate || undefined}
            onChange={(event) => onFilter(setFromDate, event.target.value)}
          />
        </Field>
        <Field>
          <FieldLabel htmlFor="audit-to">{t('auditLogs.filters.to')}</FieldLabel>
          <Input
            id="audit-to"
            type="date"
            value={toDate}
            min={fromDate || undefined}
            onChange={(event) => onFilter(setToDate, event.target.value)}
          />
        </Field>
        <Button type="button" variant="outline" onClick={clear}>
          {t('auditLogs.filters.clear')}
        </Button>
      </div>

      {logs.isPending ? (
        <p className="text-muted-foreground">{t('auditLogs.loading')}</p>
      ) : logs.data && logs.data.items.length > 0 ? (
        <AuditLogsTable entries={logs.data.items} />
      ) : (
        <p className="text-muted-foreground">{t('auditLogs.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('auditLogs.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('auditLogs.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('auditLogs.next')}
        </Button>
      </div>
    </div>
  )
}
