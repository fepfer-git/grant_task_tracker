import type { Grant, GrantTask } from './api/types'
import { formatCalendarDate } from './dates'

interface TaskListProps {
  grant: Grant
  /** The task whose Complete request is waiting, if any. Its button is disabled until the response arrives. */
  completingTaskId: string | null
  onComplete: (task: GrantTask) => void
}

/**
 * The grant's tasks, in the API's order. Overdue comes from the API's `isOverdue` (BR-6), so the UI doesn't repeat
 * the rule. Complete is offered only on pending tasks of open grants (BR-4, §4.7).
 */
export function TaskList({ grant, completingTaskId, onComplete }: TaskListProps) {
  if (grant.tasks.length === 0) {
    return <p>This grant has no tasks yet.</p>
  }

  return (
    <table className="tasks">
      <caption>Tasks</caption>
      <thead>
        <tr>
          <th scope="col">Title</th>
          <th scope="col">Due date</th>
          <th scope="col">Priority</th>
          <th scope="col">Status</th>
          <th scope="col">
            <span className="visually-hidden">Actions</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {grant.tasks.map((task) => (
          <tr key={task.id} className={task.isOverdue ? 'overdue' : undefined}>
            <td>{task.title}</td>
            <td>{formatCalendarDate(task.dueDate)}</td>
            <td>{task.priority}</td>
            <td>
              {task.status}
              {task.isOverdue && <span className="overdue-label">Overdue</span>}
            </td>
            <td>
              {grant.status === 'Open' && task.status === 'Pending' && (
                <button
                  type="button"
                  aria-label={`Complete ${task.title}`}
                  disabled={completingTaskId === task.id}
                  onClick={() => onComplete(task)}
                >
                  Complete
                </button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
