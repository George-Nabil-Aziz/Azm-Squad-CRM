import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { getSystemOverview, type SystemOverview as SystemOverviewData } from '@/api/dashboard'
import { Card, CardContent } from '@/components/ui/card'
import { ticketStatuses } from '@/features/tickets/ticket-values'
import { DashboardSection, SectionError, SectionSkeleton } from './DashboardSection'
import { StatCard } from './StatCard'
import { dashboardQueryKey } from './useDashboardData'

function Group({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <DashboardSection id={id} level={3} title={title}>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">{children}</div>
    </DashboardSection>
  )
}

function Activity({ entries }: { entries: NonNullable<SystemOverviewData['recentActivity']> }) {
  const { t, i18n } = useTranslation()
  const format = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <DashboardSection id="activity-title" level={3} title={t('dashboard.system.activityTitle')} action={{ label: t('dashboard.system.activityLink'), to: '/audit-logs' }}>
      {entries.length === 0 ? (
        <p className="text-muted-foreground">{t('dashboard.system.activityEmpty')}</p>
      ) : (
        <Card>
          <CardContent>
            <ul className="flex flex-col divide-y">
              {entries.map((entry) => (
                <li key={entry.id} className="flex flex-wrap items-center justify-between gap-2 py-2 first:pt-0 last:pb-0">
                  <span>
                    {t('dashboard.system.activityEntry', {
                      user: entry.userEmail ?? t('dashboard.system.system'),
                      action: entry.action,
                      entity: entry.entityType,
                    })}
                  </span>
                  <span className="text-sm text-muted-foreground">{format.format(new Date(entry.occurredAt))}</span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </DashboardSection>
  )
}

/** Every module at a glance (counts and links) for SuperAdmin and Admin; the API (users.manage) checks the permission too. */
export function SystemOverview() {
  const { t } = useTranslation()
  const overview = useQuery({
    queryKey: [...dashboardQueryKey, 'system-overview'],
    queryFn: ({ signal }) => getSystemOverview(signal),
    refetchInterval: 60_000,
  })
  const d = overview.data
  const s = (key: string) => t(`dashboard.system.${key}` as 'dashboard.system.customers')
  const statusCount = (status: string) => d?.ticketsByStatus.find((r) => r.key === status)?.count
  const message = (channel: 'email' | 'whatsapp' | 'sms') => d?.messages.find((m) => m.channel === channel)
  const card = (title: string, value: ReactNode | undefined, to: string, hint?: string, tone?: 'danger') => (
    <StatCard key={title} size="compact" title={title} value={value} to={to} hint={hint} tone={tone} />
  )

  return (
    <DashboardSection id="system-title" title={t('dashboard.sections.system')}>
      {overview.isPending ? <SectionSkeleton rows={4} label={t('dashboard.loading')} /> : null}
      {overview.isError ? <SectionError message={s('loadError')} /> : null}
      {d ? (
        <div className="flex flex-col gap-6">
          <Group id="system-support" title={s('groups.support')}>
            {card(s('customers'), d.customers, '/customers')}
            {ticketStatuses.map((status) => card(t(`tickets.statuses.${status}`), statusCount(status), `/tickets?status=${status}`))}
            {card(s('slaBreached'), d.slaBreachedNow, '/reports/sla', undefined, d.slaBreachedNow > 0 ? 'danger' : undefined)}
          </Group>

          <Group id="system-chat" title={s('groups.chat')}>
            {card(s('chatsWaiting'), d.chats.waiting, '/chat')}
            {card(s('chatsActive'), d.chats.active, '/chat')}
            {card(s('chatsToday'), d.chats.today, '/chat')}
          </Group>

          <Group id="system-work" title={s('groups.work')}>
            {card(s('tasksOpen'), d.tasks.open, '/tasks')}
            {card(s('tasksOverdue'), d.tasks.overdue, '/tasks', undefined, d.tasks.overdue > 0 ? 'danger' : undefined)}
            {card(s('quickPersonal'), d.quickReplies.personal, '/quick-replies')}
            {card(s('quickShared'), d.quickReplies.shared, '/quick-replies')}
            {card(s('articlesPublished'), d.kb.publishedArticles, '/knowledge-base')}
            {card(s('articlesDraft'), d.kb.draftArticles, '/knowledge-base')}
            {card(s('faqsPublished'), d.kb.publishedFaqs, '/knowledge-base')}
            {card(s('faqsDraft'), d.kb.draftFaqs, '/knowledge-base')}
            {card(s('surveysSent'), d.portal.surveysSent, '/reports/satisfaction')}
            {card(s('surveysAnswered'), d.portal.surveysAnswered, '/reports/satisfaction')}
            {card(s('portalAccounts'), d.portal.accounts, '/reports/satisfaction')}
          </Group>

          <Group id="system-people" title={s('groups.people')}>
            {d.users.byRole.map((role) => card(s(`roles.${role.key}`), role.count, '/users'))}
            {card(s('agentsOnDuty'), d.users.onDutyAgents, '/assignment', t('dashboard.system.activeOfTotal', { active: d.users.onDutyAgents, total: d.users.activeAgents }))}
            {card(s('departments'), d.departments.active, '/departments', t('dashboard.system.activeOfTotal', d.departments))}
            {card(s('branches'), d.branches.active, '/branches', t('dashboard.system.activeOfTotal', d.branches))}
          </Group>

          <Group id="system-channels" title={s('groups.channels')}>
            {card(s('webForms'), d.webFormSubmissions, '/web-forms')}
            {(['email', 'whatsapp', 'sms'] as const).map((channel) =>
              card(
                s(`messagesSent.${channel}`),
                message(channel)?.sent,
                '/settings',
                message(channel) ? t('dashboard.system.messageHint', message(channel)) : undefined,
              ),
            )}
          </Group>

          <Group id="system-integrations" title={s('groups.integrations')}>
            {card(s('apiKeys'), d.apiKeys.active, '/integrations/api-keys', t('dashboard.system.apiKeysHint', { total: d.apiKeys.total }))}
            {card(s('webhooks'), d.webhooks.enabled, '/integrations/webhooks', t('dashboard.system.webhooksHint', { total: d.webhooks.total }))}
            {card(s('webhookFailures'), d.webhooks.failedDeliveries, '/integrations/webhooks', undefined, d.webhooks.failedDeliveries > 0 ? 'danger' : undefined)}
            {card(s('erpSyncs'), d.erp.synced, '/integrations/erp-logs', d.erp.configured ? t('dashboard.system.erpHint', { failed: d.erp.failed }) : s('notConfigured'))}
            {card(s('ai'), d.ai.configured ? s('enabled') : s('notConfigured'), '/settings')}
          </Group>

          {d.recentActivity ? <Activity entries={d.recentActivity} /> : null}
        </div>
      ) : null}
    </DashboardSection>
  )
}
