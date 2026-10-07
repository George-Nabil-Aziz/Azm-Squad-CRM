import { Fragment, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { ChevronDownIcon, ChevronRightIcon } from 'lucide-react'
import type { AuditLogEntry } from '@/api/audit-logs'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { describeAuditEntry, formatIpAddress, type DiffRow } from './describeAudit'

interface AuditLogsTableProps {
  entries: AuditLogEntry[]
  /** User id to full name (the users the page already loaded), used when an entry's values carry no name. */
  userNames?: Record<string, string>
}

/** "responseMinutes" -> "Response minutes" when there is no translated label. */
function humanize(key: string): string {
  const spaced = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}

function fieldLabel(t: TFunction, path: string): string {
  return path
    .split('.')
    .map((part) => t(`auditLogs.fields.${part}`, { defaultValue: humanize(part) }))
    .join(' › ')
}

function actionKey(action: string): string {
  return action.replace(/[.-](\w)/g, (_, c: string) => c.toUpperCase())
}

/** Sentence of one entry: the description's i18n key with its values localised (priority, reason, SLA minutes). */
function sentence(t: TFunction, entry: AuditLogEntry, userNames: Record<string, string>): string {
  const { summary } = describeAuditEntry(entry, { userNames })
  const values = { ...summary.values } as Record<string, unknown>
  let key = summary.key
  if (key === 'signInFailed' && values.reason) {
    key = 'signInFailedReason'
    values.reason = t(`auditLogs.reasons.${String(values.reason)}`, { defaultValue: String(values.reason) })
  }
  if (typeof values.priority === 'string') {
    values.priority = t(`tickets.priorities.${values.priority.toLowerCase()}`, { defaultValue: values.priority })
  }
  if (Array.isArray(values.changes)) {
    values.changes = (values.changes as { part: string; from: string; to: string }[])
      .map((change) => t('auditLogs.slaChange', { part: t(`auditLogs.slaParts.${change.part}`, { defaultValue: change.part }), from: change.from, to: change.to }))
      .join(', ')
  }
  if (key === 'generic') values.action = t(`auditLogs.actions.${actionKey(entry.action)}`, { defaultValue: entry.action })
  return t(`auditLogs.summary.${key}`, { ...values, defaultValue: entry.action })
}

const kindClass: Record<DiffRow['kind'], { old: string; new: string }> = {
  added: { old: '', new: 'bg-success/10 text-success' },
  removed: { old: 'bg-destructive/10 text-destructive line-through', new: '' },
  changed: { old: 'bg-destructive/10 text-destructive line-through', new: 'bg-success/10 text-success' },
}

function DiffTable({ rows }: { rows: DiffRow[] }) {
  const { t } = useTranslation()
  if (rows.length === 0) return <p className="text-sm text-muted-foreground">{t('auditLogs.details.none')}</p>
  const empty = <span className="text-muted-foreground">{t('auditLogs.details.empty')}</span>
  return (
    <Table aria-label={t('auditLogs.details.label')}>
      <TableHeader>
        <TableRow>
          <TableHead>{t('auditLogs.details.field')}</TableHead>
          <TableHead>{t('auditLogs.details.old')}</TableHead>
          <TableHead>{t('auditLogs.details.new')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row) => (
          <TableRow key={row.field}>
            <TableCell className="font-medium whitespace-normal">{fieldLabel(t, row.field)}</TableCell>
            <TableCell className="whitespace-normal">
              {row.old === null ? empty : <span className={cn('rounded px-1.5 py-0.5 break-all', kindClass[row.kind].old)}>{row.old || empty}</span>}
            </TableCell>
            <TableCell className="whitespace-normal">
              {row.new === null ? empty : <span className={cn('rounded px-1.5 py-0.5 break-all', kindClass[row.kind].new)}>{row.new || empty}</span>}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

/** Read-only audit entries: time, user, a readable sentence with an expandable field-by-field diff, and the IP address. */
export function AuditLogsTable({ entries, userNames }: AuditLogsTableProps) {
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState<ReadonlySet<number>>(new Set())
  const formatTime = useMemo(
    () => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'medium' }),
    [i18n.language],
  )
  const names = userNames ?? {}

  function toggle(id: number) {
    setOpen((current) => {
      const next = new Set(current)
      if (!next.delete(id)) next.add(id)
      return next
    })
  }

  function ipCell(ip: string | null) {
    const formatted = formatIpAddress(ip)
    if (formatted.kind === 'address') return <span dir="ltr">{formatted.value}</span>
    return formatted.kind === 'local' ? t('auditLogs.ip.local') : '—'
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('auditLogs.columns.time')}</TableHead>
          <TableHead>{t('auditLogs.columns.user')}</TableHead>
          <TableHead>{t('auditLogs.columns.changes')}</TableHead>
          <TableHead>{t('auditLogs.columns.ip')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {entries.map((entry) => {
          const expanded = open.has(entry.id)
          return (
            <Fragment key={entry.id}>
              <TableRow>
                <TableCell className="whitespace-nowrap">{formatTime.format(new Date(entry.occurredAt))}</TableCell>
                <TableCell>{entry.userEmail ?? t('auditLogs.unknownUser')}</TableCell>
                <TableCell className="whitespace-normal">
                  <div className="flex items-center gap-2">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      aria-expanded={expanded}
                      aria-label={expanded ? t('auditLogs.details.hide') : t('auditLogs.details.show')}
                      onClick={() => toggle(entry.id)}
                    >
                      {expanded ? <ChevronDownIcon aria-hidden /> : <ChevronRightIcon className="rtl:rotate-180" aria-hidden />}
                    </Button>
                    <span>{sentence(t, entry, names)}</span>
                  </div>
                </TableCell>
                <TableCell className="whitespace-nowrap">{ipCell(entry.ipAddress)}</TableCell>
              </TableRow>
              {expanded ? (
                <TableRow>
                  <TableCell colSpan={4} className="bg-muted/30 whitespace-normal">
                    <DiffTable rows={describeAuditEntry(entry, { userNames: names }).diff} />
                  </TableCell>
                </TableRow>
              ) : null}
            </Fragment>
          )
        })}
      </TableBody>
    </Table>
  )
}
