import { useQuery } from '@tanstack/react-query'
import { listDepartments, listDepartmentSlaPolicies, type DepartmentListParams } from '@/api/departments'

/** Prefix of every departments query: mutations invalidate it so every list reloads. */
export const departmentsQueryKey = ['departments'] as const

/** GET /api/departments (all departments, or only the active ones). */
export function useDepartments(params: DepartmentListParams, enabled = true) {
  return useQuery({
    queryKey: [...departmentsQueryKey, params],
    queryFn: ({ signal }) => listDepartments(params, signal),
    enabled,
  })
}

/** GET /api/departments/{id}/sla-policies. */
export function useDepartmentSlaPolicies(departmentId: string) {
  return useQuery({
    queryKey: [...departmentsQueryKey, 'sla', departmentId],
    queryFn: ({ signal }) => listDepartmentSlaPolicies(departmentId, signal),
  })
}
