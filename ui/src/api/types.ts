// The API's JSON, as DESIGN.md §5 describes it. Field names match the API's camelCase JSON exactly.

/**
 * A calendar date, "YYYY-MM-DD", exactly as the API sends it (DESIGN.md §3.6, §5). It's kept as text and never
 * turned into a JavaScript Date: a Date reads it as midnight UTC, which is the previous day west of UTC.
 * Two calendar dates compare correctly as strings.
 */
export type CalendarDate = string

/** An instant in UTC, ISO 8601 with a "Z" suffix, for example "2026-03-10T22:00:00Z". */
export type UtcInstant = string

export type GrantStatus = 'Open' | 'Closed'
export type GrantTaskStatus = 'Pending' | 'Completed' | 'Cancelled'
export type TaskPriority = 'Low' | 'Medium' | 'High'

/** `GET /grants` */
export interface GrantListItem {
  id: string
  reference: string
  organisationName: string
  applicationDate: CalendarDate
  status: GrantStatus
  closedAtUtc: UtcInstant | null
  pendingTaskCount: number
}

/** `GET /grants/{id}` and `PUT /grants/{id}/close`. `today` is the API's "today" in Sydney (DESIGN.md §4.0). */
export interface Grant extends GrantListItem {
  closedNote: string | null
  today: CalendarDate
  tasks: GrantTask[]
}

/** A task, as in `Grant.tasks`, `POST /grants/{id}/tasks` and `PUT /tasks/{id}/complete`. */
export interface GrantTask {
  id: string
  grantId: string
  title: string
  dueDate: CalendarDate
  priority: TaskPriority
  status: GrantTaskStatus
  isOverdue: boolean
  createdAtUtc: UtcInstant
  completedAtUtc: UtcInstant | null
  cancelledAtUtc: UtcInstant | null
}

/** `GET /tasks/overdue` */
export interface OverdueTask {
  id: string
  grantId: string
  grantReference: string
  organisationName: string
  title: string
  dueDate: CalendarDate
  priority: TaskPriority
}

/** Body of `POST /grants/{id}/tasks`. The client chooses `id`, so sending it again is safe (DESIGN.md §6.3). */
export interface CreateTaskRequest {
  id: string
  title: string
  dueDate: CalendarDate
  priority: TaskPriority
}

/** Body of `PUT /grants/{id}/close`. The note is optional. */
export interface CloseGrantRequest {
  note?: string
}

/**
 * Every error body (RFC 9457, DESIGN.md §5.1). The UI decides what happened from `code`, shows `title` and `detail`,
 * and shows `errors` on the form fields.
 */
export interface ProblemDetails {
  type?: string
  code?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Field errors for a 400, keyed by the request's field name, for example `title` or `dueDate`. */
  errors?: Record<string, string[]>
  /** BR-5: the pending High tasks that stop the grant from closing. */
  blockingTaskIds?: string[]
}
