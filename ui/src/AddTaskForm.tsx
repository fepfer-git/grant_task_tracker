import { useState, type FormEvent, type ReactNode } from 'react'
import { addTask } from './api/grants'
import type { CalendarDate, Grant, ProblemDetails, TaskPriority } from './api/types'
import { ProblemMessage } from './ProblemMessage'

/** BR-3, counted like the API: JavaScript `.length` and .NET `string.Length` both count UTF-16 code units. */
export const TITLE_MAX_LENGTH = 100

const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High']
const DEFAULT_PRIORITY: TaskPriority = 'Medium'

type Field = 'title' | 'dueDate' | 'priority'
type FieldErrors = Partial<Record<Field, string[]>>

/**
 * The client-side checks (BR-2, BR-3), with the same messages as the API. Dates are compared as "YYYY-MM-DD"
 * strings, and "today" is the API's, not the browser's (DESIGN.md §4.0). The API is still the authority.
 */
export function validateTask(title: string, dueDate: CalendarDate, grant: Pick<Grant, 'today' | 'applicationDate'>) {
  const errors: FieldErrors = {}

  const trimmed = title.trim()
  if (trimmed.length === 0) {
    errors.title = ['Title is required.']
  } else if (trimmed.length > TITLE_MAX_LENGTH) {
    errors.title = [`Title must be ${TITLE_MAX_LENGTH} characters or fewer.`]
  }

  if (dueDate === '') {
    errors.dueDate = ['Due date is required.']
  } else {
    const messages: string[] = []
    if (dueDate < grant.today) {
      messages.push('Due date cannot be in the past.')
    }
    if (dueDate < grant.applicationDate) {
      messages.push("Due date cannot be before the grant's application date.")
    }
    if (messages.length > 0) {
      errors.dueDate = messages
    }
  }

  return errors
}

interface AddTaskFormProps {
  grant: Grant
  /** Called when a request starts, so the screen can clear an old banner. */
  onRequestStart: () => void
  /** Reload the grant: after a success, and after any 409 (the grant changed). */
  onChanged: () => Promise<void>
  onServerProblem: () => void
}

/**
 * Add task (CONTEXT §7, DESIGN.md §6.3). Render it with `key={grant.id}`, so switching grants starts a fresh form
 * with a fresh task id.
 */
export function AddTaskForm({ grant, onRequestStart, onChanged, onServerProblem }: AddTaskFormProps) {
  // The task id is chosen here, so sending the same task again is safe (OP-3). It's kept until the server confirms
  // the task, so a resend after a timeout uses the same id.
  const [taskId, setTaskId] = useState(() => crypto.randomUUID())
  const [title, setTitle] = useState('')
  const [dueDate, setDueDate] = useState<CalendarDate>('')
  const [priority, setPriority] = useState<TaskPriority>(DEFAULT_PRIORITY)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [savedTitle, setSavedTitle] = useState<string | null>(null)
  const [sending, setSending] = useState(false)

  const closed = grant.status === 'Closed'

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setProblem(null)
    setSavedTitle(null)

    const errors = validateTask(title, dueDate, grant)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      return
    }

    onRequestStart()
    setSending(true)
    const result = await addTask(grant.id, { id: taskId, title: title.trim(), dueDate, priority })
    setSending(false)

    switch (result.kind) {
      case 'success':
        setSavedTitle(result.data.title)
        setTaskId(crypto.randomUUID())
        setTitle('')
        setDueDate('')
        setPriority(DEFAULT_PRIORITY)
        await onChanged()
        return

      case 'api-error': {
        const fromApi = fieldErrorsFrom(result.problem)
        if (fromApi === null) {
          setProblem(result.problem)
        } else {
          setFieldErrors(fromApi)
        }
        if (result.problem.code === 'duplicate-task-id') {
          // The id belongs to a task that was already saved (DESIGN.md §6.3). The typed values are kept.
          setTaskId(crypto.randomUUID())
        }
        if (result.status === 409) {
          await onChanged()
        }
        return
      }

      case 'server-problem':
        // The typed values and the task id are kept, so sending again is safe.
        onServerProblem()
        return
    }
  }

  return (
    <form className="add-task" onSubmit={handleSubmit} noValidate>
      <h3>Add task</h3>
      {closed && <p>This grant is closed, so tasks can't be added.</p>}
      <fieldset disabled={closed || sending}>
        <legend className="visually-hidden">New task</legend>

        <FormField id="task-title" label="Title" errors={fieldErrors.title}>
          <input
            id="task-title"
            type="text"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            aria-invalid={fieldErrors.title !== undefined}
            aria-describedby={fieldErrors.title === undefined ? undefined : 'task-title-error'}
          />
        </FormField>

        <FormField id="task-due-date" label="Due date" errors={fieldErrors.dueDate}>
          <input
            id="task-due-date"
            type="date"
            value={dueDate}
            min={grant.today > grant.applicationDate ? grant.today : grant.applicationDate}
            onChange={(event) => setDueDate(event.target.value)}
            aria-invalid={fieldErrors.dueDate !== undefined}
            aria-describedby={fieldErrors.dueDate === undefined ? undefined : 'task-due-date-error'}
          />
        </FormField>

        <FormField id="task-priority" label="Priority" errors={fieldErrors.priority}>
          <select
            id="task-priority"
            value={priority}
            onChange={(event) => setPriority(event.target.value as TaskPriority)}
            aria-invalid={fieldErrors.priority !== undefined}
            aria-describedby={fieldErrors.priority === undefined ? undefined : 'task-priority-error'}
          >
            {PRIORITIES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </FormField>

        <button type="submit">{sending ? 'Adding…' : 'Add task'}</button>
      </fieldset>

      {problem !== null && <ProblemMessage problem={problem} />}
      {savedTitle !== null && <p role="status">Task "{savedTitle}" added.</p>}
    </form>
  )
}

function FormField({ id, label, errors, children }: {
  id: string
  label: string
  errors: string[] | undefined
  children: ReactNode
}) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children}
      {errors !== undefined && (
        <p id={`${id}-error`} className="field-error">
          {errors.join(' ')}
        </p>
      )}
    </div>
  )
}

/**
 * A 400's field errors, keyed by the form's fields (DESIGN.md §5.1). Null when the problem has no field the form
 * shows (for example a 409, or an error keyed on `id`), so the problem's title and detail are shown instead.
 */
function fieldErrorsFrom(problem: ProblemDetails): FieldErrors | null {
  if (problem.errors === undefined) {
    return null
  }

  const errors: FieldErrors = {}
  for (const field of ['title', 'dueDate', 'priority'] as const) {
    const messages = problem.errors[field]
    if (messages !== undefined && messages.length > 0) {
      errors[field] = messages
    }
  }

  return Object.keys(errors).length > 0 ? errors : null
}
