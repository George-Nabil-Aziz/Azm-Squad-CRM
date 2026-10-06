import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getAssignmentSettings, setAgentOnDuty, setAutoAssign, type AssignmentSettings } from '@/api/assignment-settings'
import { Checkbox } from '@/components/ui/checkbox'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const assignmentSettingsQueryKey = ['assignment-settings'] as const

/** Automatic assignment (supervisors): the on / off switch and which agents are on duty. */
export function AssignmentSettingsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const settings = useQuery({
    queryKey: assignmentSettingsQueryKey,
    queryFn: ({ signal }) => getAssignmentSettings(signal),
  })

  const onSaved = (saved: AssignmentSettings) => queryClient.setQueryData(assignmentSettingsQueryKey, saved)
  const toggleAuto = useMutation({ mutationFn: (enabled: boolean) => setAutoAssign(enabled), onSuccess: onSaved })
  const toggleDuty = useMutation({
    mutationFn: ({ id, onDuty }: { id: string; onDuty: boolean }) => setAgentOnDuty(id, onDuty),
    onSuccess: onSaved,
  })

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.assignment')}</h1>
        <p className="text-muted-foreground">{t('assignment.description')}</p>
      </div>

      {settings.isPending ? (
        <p className="text-muted-foreground">{t('assignment.loading')}</p>
      ) : settings.data ? (
        <>
          <div className="flex items-center gap-2">
            <Checkbox
              id="auto-assign"
              checked={settings.data.autoAssignEnabled}
              disabled={toggleAuto.isPending}
              onCheckedChange={(checked) => toggleAuto.mutate(checked === true)}
            />
            <label htmlFor="auto-assign" className="text-sm font-medium">
              {t('assignment.autoAssign')}
            </label>
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('assignment.columns.agent')}</TableHead>
                <TableHead>{t('assignment.columns.openTickets')}</TableHead>
                <TableHead>{t('assignment.columns.onDuty')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {settings.data.agents.map((agent) => (
                <TableRow key={agent.id}>
                  <TableCell className="font-medium">{agent.fullName}</TableCell>
                  <TableCell>{agent.openTickets}</TableCell>
                  <TableCell>
                    <Checkbox
                      aria-label={t('assignment.onDutyFor', { name: agent.fullName })}
                      checked={agent.onDuty}
                      disabled={toggleDuty.isPending}
                      onCheckedChange={(checked) => toggleDuty.mutate({ id: agent.id, onDuty: checked === true })}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {settings.data.agents.length === 0 ? <p className="text-muted-foreground">{t('assignment.noAgents')}</p> : null}
        </>
      ) : null}
    </div>
  )
}
