import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { deactivateUser, reactivateUser, type User } from '@/api/users'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { usersQueryKey } from './useUsers'

/** Deactivate button (asks for confirmation first) for an active user, reactivate button for an inactive one. */
export function UserStatusAction({ user }: { user: User }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const change = useMutation({
    mutationFn: () => (user.isActive ? deactivateUser(user.id) : reactivateUser(user.id)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: usersQueryKey })
      toast.success(t(user.isActive ? 'users.deactivated' : 'users.reactivated', { name: user.fullName }))
    },
  })

  if (!user.isActive) {
    return (
      <Button variant="outline" size="sm" disabled={change.isPending} onClick={() => change.mutate()}>
        {t('users.reactivate')}
      </Button>
    )
  }

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="outline" size="sm" disabled={change.isPending}>
          {t('users.deactivate')}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t('users.deactivateTitle', { name: user.fullName })}</AlertDialogTitle>
          <AlertDialogDescription>{t('users.deactivateDescription')}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t('users.cancel')}</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={() => change.mutate()}>
            {t('users.deactivate')}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
