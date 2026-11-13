import { apiFetch } from './client'
import type { CreateTaskRequest, Grant, GrantListItem, GrantTask } from './types'

// One function per endpoint the grant screen uses (DESIGN.md §5). Each returns an ApiResult and never throws.

export const listGrants = () => apiFetch<GrantListItem[]>('/grants')

export const getGrant = (grantId: string) => apiFetch<Grant>(`/grants/${encodeURIComponent(grantId)}`)

/** 201 for a new task, 200 when the same task was already created. The UI treats both the same (§6.3). */
export const addTask = (grantId: string, request: CreateTaskRequest) =>
  apiFetch<GrantTask>(`/grants/${encodeURIComponent(grantId)}/tasks`, { method: 'POST', body: request })

export const completeTask = (taskId: string) =>
  apiFetch<GrantTask>(`/tasks/${encodeURIComponent(taskId)}/complete`, { method: 'PUT' })
