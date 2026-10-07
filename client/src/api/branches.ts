import { apiGet, apiPost, apiPut } from './client'

/** A branch (server: BranchResponse). Inactive branches stay on existing customers, tickets and users only. */
export interface Branch {
  id: string
  name: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

/** Body of create and edit. Names are unique ignoring case and spaces (the server answers 400 otherwise). */
export interface BranchRequest {
  name: string
  isActive: boolean
}

export interface BranchListParams {
  /** Only the branches that can be chosen for new users and customers. */
  activeOnly?: boolean
}

/** GET /api/branches: every branch ordered by name (a branch user only gets their own). */
export function listBranches({ activeOnly }: BranchListParams, signal?: AbortSignal): Promise<Branch[]> {
  return apiGet<Branch[]>(activeOnly ? '/api/branches?activeOnly=true' : '/api/branches', signal)
}

export function createBranch(request: BranchRequest): Promise<Branch> {
  return apiPost<Branch>('/api/branches', request)
}

export function updateBranch(id: string, request: BranchRequest): Promise<Branch> {
  return apiPut<Branch>(`/api/branches/${encodeURIComponent(id)}`, request)
}

/** Assigns the user to a branch, or removes the branch (null). Needs branches.manage (SuperAdmin). */
export function setUserBranch(userId: string, branchId: string | null): Promise<unknown> {
  return apiPut<unknown>(`/api/users/${encodeURIComponent(userId)}/branch`, { branchId })
}
