import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { exportAgentReport, type ExportFormat } from '@/api/reports'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { DateRangeFields } from '@/features/reports/DateRangeFields'
import { ExportButtons } from '@/features/reports/ExportButtons'
import { useAgentReport } from '@/features/reports/useAgentReport'
import { formatMinutes } from '@/features/sla/sla-format'
import { saveFile } from '@/lib/save-file'

const NONE = '–'

/** Agent performance: tickets handled, average times, SLA % and average CSAT per agent, with export. */
export function AgentReportPage() {
  const { t } = useTranslation()
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const params = { ...(from ? { from } : {}), ...(to ? { to } : {}) }
  const report = useAgentReport(params)
  const data = report.data
  const minutes = (value: number | null) => (value === null ? NONE : formatMinutes(Math.round(value), t))

  async function onExport(format: ExportFormat) {
    const blob = await exportAgentReport(params, format)
    saveFile(blob, `agent-report-${data ? `${data.from}_${data.to}` : 'export'}.${format}`)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <DateRangeFields from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
      </div>
      <ExportButtons onExport={onExport} />
      <Card>
        <CardHeader>
          <CardTitle>{t('reports.agents.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          {!data ? (
            <p className="text-muted-foreground">{t('reports.loading')}</p>
          ) : data.agents.length === 0 ? (
            <p className="text-muted-foreground">{t('reports.agents.empty')}</p>
          ) : (
            <>
              <p className="mb-3 text-sm text-muted-foreground">{t('reports.agents.scope', { from: data.from, to: data.to })}</p>
              <Table aria-label={t('reports.agents.title')}>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('reports.agents.agent')}</TableHead>
                    <TableHead>{t('reports.agents.handled')}</TableHead>
                    <TableHead>{t('reports.sla.avgResponse')}</TableHead>
                    <TableHead>{t('reports.sla.avgResolution')}</TableHead>
                    <TableHead>{t('reports.agents.sla')}</TableHead>
                    <TableHead>{t('reports.agents.csat')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.agents.map((agent) => (
                    <TableRow key={agent.agentId}>
                      <TableCell className="font-medium">{agent.name}</TableCell>
                      <TableCell className="tabular-nums">{agent.ticketsHandled}</TableCell>
                      <TableCell className="tabular-nums">{minutes(agent.averageFirstResponseMinutes)}</TableCell>
                      <TableCell className="tabular-nums">{minutes(agent.averageResolutionMinutes)}</TableCell>
                      <TableCell className="tabular-nums">{agent.slaPercent === null ? NONE : `${agent.slaPercent}%`}</TableCell>
                      <TableCell className="tabular-nums">{agent.averageCsat ?? NONE}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
