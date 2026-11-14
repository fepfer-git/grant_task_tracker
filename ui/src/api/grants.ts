import { apiFetch } from './client'
import type { CloseGrantRequest, CreateTaskRequest, Grant, GrantListItem, GrantTask, OverdueTask } from './types'

// One function per endpoint the UI uses (DESIGN.md §5). Each returns an ApiResult and never throws.

export const listGrants = () => apiFetch<GrantListItem[]>('/grants')

export const getGrant = (grantId: string) => apiFetch<Grant>(`/grants/${encodeURIComponent(grantId)}`)

/** 201 for a new task, 200 when the same task was already created. The UI treats both the same (§6.3). */
export const addTask = (grantId: string, request: CreateTaskRequest) =>
  apiFetch<GrantTask>(`/grants/${encodeURIComponent(grantId)}/tasks`, { method: 'POST', body: request })

export const completeTask = (taskId: string) =>
  apiFetch<GrantTask>(`/tasks/${encodeURIComponent(taskId)}/complete`, { method: 'PUT' })

/** Pending tasks past their due date across all grants, most overdue first (BR-6). */
export const listOverdueTasks = () => apiFetch<OverdueTask[]>('/tasks/overdue')

/** 200 with the closed grant, also when it was already closed (BR-5). 409 BR-5 while a High task is pending. */
export const closeGrant = (grantId: string, request: CloseGrantRequest) =>
  apiFetch<Grant>(`/grants/${encodeURIComponent(grantId)}/close`, { method: 'PUT', body: request })
