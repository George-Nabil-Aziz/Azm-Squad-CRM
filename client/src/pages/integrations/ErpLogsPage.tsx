import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { listErpSyncLogs } from '@/api/integrations'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const resultLabelKey = {
  success: 'integrations.erpLogs.results.success',
  failed: 'integrations.erpLogs.results.failed',
  not_configured: 'integrations.erpLogs.results.not_configured',
} as const

/** The ERP sync log (CRM-60): result and time of every read of a customer's ERP data. */
export function ErpLogsPage() {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const logs = useQuery({ queryKey: ['erp-sync-logs', page], queryFn: ({ signal }) => listErpSyncLogs(page, signal) })
  const lastPage = logs.data ? Math.max(1, Math.ceil(logs.data.totalCount / logs.data.pageSize)) : 1

  return (
    <div className="flex flex-col gap-4">
      <p className="text-muted-foreground">{t('integrations.erpLogs.description')}</p>
      {logs.isPending ? <p className="text-muted-foreground">{t('integrations.erpLogs.loading')}</p> : null}
      {logs.data?.items.length === 0 ? <p className="text-muted-foreground">{t('integrations.erpLogs.empty')}</p> : null}
      {logs.data && logs.data.items.length > 0 ? (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('integrations.erpLogs.time')}</TableHead>
                <TableHead>{t('integrations.erpLogs.customer')}</TableHead>
                <TableHead>{t('integrations.erpLogs.erpId')}</TableHead>
                <TableHead>{t('integrations.erpLogs.result')}</TableHead>
                <TableHead>{t('integrations.erpLogs.error')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {logs.data.items.map((log) => (
                <TableRow key={log.id}>
                  <TableCell>{new Date(log.createdAt).toLocaleString()}</TableCell>
                  <TableCell>{log.customerName}</TableCell>
                  <TableCell dir="ltr" className="text-start">{log.erpCustomerId}</TableCell>
                  <TableCell>{t(resultLabelKey[log.result])}</TableCell>
                  <TableCell dir="auto">{log.error}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className="flex gap-2">
            <Button type="button" variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              {t('integrations.erpLogs.previous')}
            </Button>
            <Button type="button" variant="outline" size="sm" disabled={page >= lastPage} onClick={() => setPage(page + 1)}>
              {t('integrations.erpLogs.next')}
            </Button>
          </div>
        </>
      ) : null}
    </div>
  )
}
