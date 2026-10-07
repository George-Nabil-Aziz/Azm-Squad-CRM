import { useTranslation } from 'react-i18next'
import { Card, CardContent } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAgentReport } from '@/features/reports/useAgentReport'
import { DashboardSection, SectionError, SectionSkeleton } from './DashboardSection'

const TOP_AGENTS = 5
const NONE = '–'

/** The agents with the most tickets handled in the last 30 days, with SLA % and CSAT. */
export function Team() {
  const { t } = useTranslation()
  const report = useAgentReport({})
  const top = report.data ? [...report.data.agents].sort((a, b) => b.ticketsHandled - a.ticketsHandled).slice(0, TOP_AGENTS) : []

  return (
    <DashboardSection
      id="team-title"
      title={t('dashboard.sections.team')}
      action={{ label: t('dashboard.team.agentReport'), to: '/reports/agents' }}
    >
      {report.isPending ? <SectionSkeleton label={t('dashboard.loading')} /> : null}
      {report.isError ? <SectionError message={t('dashboard.team.loadError')} /> : null}
      {report.data && top.length === 0 ? <p className="text-muted-foreground">{t('dashboard.team.empty')}</p> : null}
      {top.length > 0 ? (
        <Card>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('dashboard.team.agent')}</TableHead>
                  <TableHead>{t('dashboard.team.handled')}</TableHead>
                  <TableHead>{t('dashboard.team.sla')}</TableHead>
                  <TableHead>{t('dashboard.team.csat')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {top.map((agent) => (
                  <TableRow key={agent.agentId}>
                    <TableCell className="font-medium">{agent.name}</TableCell>
                    <TableCell className="tabular-nums">{agent.ticketsHandled}</TableCell>
                    <TableCell className="tabular-nums">{agent.slaPercent === null ? NONE : `${agent.slaPercent}%`}</TableCell>
                    <TableCell className="tabular-nums">{agent.averageCsat === null ? NONE : agent.averageCsat}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      ) : null}
    </DashboardSection>
  )
}
