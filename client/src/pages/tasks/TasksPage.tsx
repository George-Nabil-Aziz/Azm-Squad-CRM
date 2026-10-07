import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { createTask, listTasks, markTaskDone } from '@/api/tasks'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const tasksQueryKey = ['tasks'] as const

/** My tasks and reminders (CRM-31): create a task with a due time and mark open tasks done. */
export function TasksPage() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const tasks = useQuery({ queryKey: tasksQueryKey, queryFn: ({ signal }) => listTasks('open', signal) })
  const [title, setTitle] = useState('')
  const [due, setDue] = useState('')
  const [errors, setErrors] = useState<{ title?: string; dueAt?: string }>({})
  const reload = () => queryClient.invalidateQueries({ queryKey: tasksQueryKey })

  const create = useMutation({ mutationFn: createTask, onSuccess: reload })
  const done = useMutation({ mutationFn: (id: string) => markTaskDone(id), onSuccess: reload })
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  async function submit(event: FormEvent) {
    event.preventDefault()
    const next: { title?: string; dueAt?: string } = {}
    if (title.trim() === '') next.title = t('tasks.titleRequired')
    const dueDate = new Date(due)
    if (due === '' || Number.isNaN(dueDate.getTime()) || dueDate.getTime() <= Date.now()) next.dueAt = t('tasks.dueInPast')
    setErrors(next)
    if (next.title || next.dueAt) return
    try {
      await create.mutateAsync({ title: title.trim(), dueAt: dueDate.toISOString() })
      setTitle('')
      setDue('')
    } catch (caught) {
      const problem = isApiError(caught) ? caught.problem?.errors : undefined
      setErrors({ title: problem?.title?.[0], dueAt: problem?.dueAt?.[0] })
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.tasks')}</h1>
        <p className="text-muted-foreground">{t('tasks.description')}</p>
      </div>

      <form onSubmit={(event) => void submit(event)} noValidate className="flex flex-col items-stretch gap-3 sm:flex-row sm:flex-wrap sm:items-start">
        <div className="flex flex-col gap-1">
          <label htmlFor="task-title" className="text-sm font-medium">
            {t('tasks.title')}
          </label>
          <Input id="task-title" className="w-full" value={title} aria-invalid={errors.title ? true : undefined} onChange={(e) => setTitle(e.target.value)} />
          {errors.title ? <p role="alert" className="text-sm text-destructive">{errors.title}</p> : null}
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="task-due" className="text-sm font-medium">
            {t('tasks.dueAt')}
          </label>
          <Input id="task-due" className="w-full" type="datetime-local" value={due} aria-invalid={errors.dueAt ? true : undefined} onChange={(e) => setDue(e.target.value)} />
          {errors.dueAt ? <p role="alert" className="text-sm text-destructive">{errors.dueAt}</p> : null}
        </div>
        <Button type="submit" className="sm:mt-6" disabled={create.isPending}>
          {t('tasks.add')}
        </Button>
      </form>

      {tasks.isPending ? <p className="text-muted-foreground">{t('tasks.loading')}</p> : null}
      {tasks.data?.length === 0 ? <p className="text-muted-foreground">{t('tasks.empty')}</p> : null}
      {tasks.data && tasks.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('tasks.title')}</TableHead>
              <TableHead>{t('tasks.dueAt')}</TableHead>
              <TableHead>{t('tasks.ticket')}</TableHead>
              <TableHead className="text-end">{t('tasks.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {tasks.data.map((task) => (
              <TableRow key={task.id}>
                <TableCell className="font-medium">{task.title}</TableCell>
                <TableCell>
                  <time dateTime={task.dueAt}>{formatTime.format(new Date(task.dueAt))}</time>
                </TableCell>
                <TableCell dir="ltr" className="text-start">
                  {task.ticketId ? (
                    <Link to={`/tickets/${task.ticketId}`} className="text-primary underline-offset-4 hover:underline">
                      {task.ticketNumber}
                    </Link>
                  ) : null}
                </TableCell>
                <TableCell>
                  <div className="flex justify-end">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={done.isPending}
                      aria-label={t('tasks.markDoneFor', { title: task.title })}
                      onClick={() => done.mutate(task.id)}
                    >
                      {t('tasks.markDone')}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
    </div>
  )
}
