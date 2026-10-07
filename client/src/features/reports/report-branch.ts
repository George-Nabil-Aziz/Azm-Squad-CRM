import { createContext, useContext } from 'react'

/** The branch the reports are filtered by (CRM-62): chosen in ReportsLayout, read by every report hook. undefined = every branch. */
export const ReportBranchContext = createContext<string | undefined>(undefined)

export function useReportBranch(): string | undefined {
  return useContext(ReportBranchContext)
}

/** `params` with the chosen branch added (unchanged when none is chosen). */
export function withBranch<T extends object>(params: T, branchId: string | undefined): T & { branchId?: string } {
  return branchId ? { ...params, branchId } : params
}
