import { useTranslation } from 'react-i18next'
import type { User } from '@/api/users'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { usePermissions } from '@/features/auth/usePermissions'
import { UserStatusAction } from './UserStatusAction'

interface UsersTableProps {
  users: User[]
  onEdit: (user: User) => void
}

export function UsersTable({ users, onEdit }: UsersTableProps) {
  const { t, i18n } = useTranslation()
  const { can } = usePermissions()
  // A SuperAdmin account can only be changed by a SuperAdmin (the API answers 403 to everyone else).
  const mayChange = (user: User) => !user.roles.includes('SuperAdmin') || can(permissions.usersManageSuperAdmins)
  // List punctuation of the UI language (comma + space in English, Arabic conjunction in Arabic).
  const roleList = new Intl.ListFormat(i18n.language, { type: 'unit' })

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('users.columns.name')}</TableHead>
          <TableHead>{t('users.columns.email')}</TableHead>
          <TableHead>{t('users.columns.roles')}</TableHead>
          <TableHead>{t('users.columns.status')}</TableHead>
          <TableHead className="text-end">{t('users.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {users.map((user) => (
          <TableRow key={user.id}>
            <TableCell className="font-medium">{user.fullName}</TableCell>
            <TableCell>{user.email}</TableCell>
            <TableCell>{roleList.format(user.roles.map((role) => t(`users.roleNames.${role}`)))}</TableCell>
            <TableCell>
              <Badge variant={user.isActive ? 'secondary' : 'outline'}>
                {t(user.isActive ? 'users.active' : 'users.inactive')}
              </Badge>
            </TableCell>
            <TableCell>
              {mayChange(user) ? (
                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={() => onEdit(user)}>
                    {t('users.edit')}
                  </Button>
                  <UserStatusAction user={user} />
                </div>
              ) : null}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
