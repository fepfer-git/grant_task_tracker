import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { listOverdueTasks } from './api/grants'
import type { OverdueTask, ProblemDetails } from './api/types'
import { formatCalendarDate } from './dates'
import { ProblemMessage, ServerProblemBanner } from './ProblemMessage'

type ListState =
  | { kind: 'loading' }
  | { kind: 'loaded'; tasks: OverdueTask[] }
  | { kind: 'api-error'; problem: ProblemDetails }
  | { kind: 'server-problem' }

interface OverdueTasksProps {
  /** Shows the grant on the screen. The dialog closes first. */
  onOpenGrant: (grantId: string) => void
}

/**
 * The "Show overdue tasks" button and its dialog (DESIGN.md BR-6 UI): every overdue task across all grants, most
 * overdue first, as `GET /tasks/overdue` returns them. The list is loaded each time the dialog opens, so it's current.
 * It's read-only: "Open grant" shows the task's grant, where it can be completed.
 */
export function OverdueTasks({ onOpenGrant }: OverdueTasksProps) {
  const [open, setOpen] = useState(false)
  const [state, setState] = useState<ListState>({ kind: 'loading' })
  const openButtonRef = useRef<HTMLButtonElement>(null)

  // When the dialog closes, focus goes back to the button that opened it. The button is enabled again only after the
  // render, so this runs as an effect.
  const wasOpenRef = useRef(false)
  useEffect(() => {
    if (wasOpenRef.current && !open) {
      openButtonRef.current?.focus()
    }
    wasOpenRef.current = open
  }, [open])

  // Counts the loads, so a response that arrives after a newer load started (the dialog was closed and shown again, or
  // Try again was selected) is ignored.
  const loadNumberRef = useRef(0)

  async function load() {
    const loadNumber = ++loadNumberRef.current
    setState({ kind: 'loading' })
    const result = await listOverdueTasks()
    if (loadNumber !== loadNumberRef.current) {
      return
    }

    if (result.kind === 'success') {
      setState({ kind: 'loaded', tasks: result.data })
    } else if (result.kind === 'server-problem') {
      setState({ kind: 'server-problem' })
    } else {
      setState({ kind: 'api-error', problem: result.problem })
    }
  }

  function openDialog() {
    setOpen(true)
    void load()
  }

  function closeDialog() {
    setOpen(false)
  }

  function openGrant(grantId: string) {
    closeDialog()
    onOpenGrant(grantId)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      closeDialog()
    }
  }

  return (
    <>
      <button type="button" ref={openButtonRef} onClick={openDialog} disabled={open}>
        Show overdue tasks
      </button>

      {open && (
        <div className="modal-backdrop">
          <div role="dialog" aria-modal="true" aria-labelledby="overdue-heading" className="dialog modal" onKeyDown={handleKeyDown}>
            <div className="modal-header">
              <h2 id="overdue-heading">Overdue tasks</h2>
              <button type="button" onClick={closeDialog} autoFocus>
                Close
              </button>
            </div>

            {state.kind === 'loading' && <p>Loading overdue tasks…</p>}

            {state.kind === 'server-problem' && (
              <>
                <ServerProblemBanner />
                <button type="button" onClick={() => void load()}>
                  Try again
                </button>
              </>
            )}

            {state.kind === 'api-error' && <ProblemMessage problem={state.problem} />}

            {state.kind === 'loaded' && state.tasks.length === 0 && <p>There are no overdue tasks.</p>}

            {state.kind === 'loaded' && state.tasks.length > 0 && (
              <table className="tasks">
                <caption className="visually-hidden">Overdue tasks, most overdue first</caption>
                <thead>
                  <tr>
                    <th scope="col">Grant</th>
                    <th scope="col">Task</th>
                    <th scope="col">Due date</th>
                    <th scope="col">Priority</th>
                    <th scope="col">
                      <span className="visually-hidden">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {state.tasks.map((task) => (
                    <tr key={task.id}>
                      <td>
                        {task.grantReference} – {task.organisationName}
                      </td>
                      <td>{task.title}</td>
                      <td>{formatCalendarDate(task.dueDate)}</td>
                      <td>{task.priority}</td>
                      <td>
                        <button type="button" aria-label={`Open grant for ${task.title}`} onClick={() => openGrant(task.grantId)}>
                          Open grant
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </>
  )
}
