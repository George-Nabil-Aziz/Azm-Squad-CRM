import { useTranslation } from 'react-i18next'
import { auditActionKeys, type AuditLogEntry } from '@/api/audit-logs'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

interface AuditLogsTableProps {
  entries: AuditLogEntry[]
}

/** Read-only table of audit entries: time, user, action, entity, old/new values, IP. */
export function AuditLogsTable({ entries }: AuditLogsTableProps) {
  const { t, i18n } = useTranslation()
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'medium' })

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('auditLogs.columns.time')}</TableHead>
          <TableHead>{t('auditLogs.columns.user')}</TableHead>
          <TableHead>{t('auditLogs.columns.action')}</TableHead>
          <TableHead>{t('auditLogs.columns.entity')}</TableHead>
          <TableHead>{t('auditLogs.columns.oldValues')}</TableHead>
          <TableHead>{t('auditLogs.columns.newValues')}</TableHead>
          <TableHead>{t('auditLogs.columns.ip')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {entries.map((entry) => (
          <TableRow key={entry.id}>
            <TableCell className="whitespace-nowrap">{formatTime.format(new Date(entry.occurredAt))}</TableCell>
            <TableCell>{entry.userEmail ?? t('auditLogs.unknownUser')}</TableCell>
            <TableCell>{t(`auditLogs.actions.${auditActionKeys[entry.action]}`, { defaultValue: entry.action })}</TableCell>
            <TableCell>
              {entry.entityType}
              {entry.entityId ? <span className="block text-xs text-muted-foreground">{entry.entityId}</span> : null}
            </TableCell>
            <TableCell className="max-w-64 font-mono text-xs break-all whitespace-normal">{entry.oldValues}</TableCell>
            <TableCell className="max-w-64 font-mono text-xs break-all whitespace-normal">{entry.newValues}</TableCell>
            <TableCell className="whitespace-nowrap">{entry.ipAddress}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
