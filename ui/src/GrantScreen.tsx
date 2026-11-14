import { useEffect, useRef, useState } from 'react'
import { completeTask, getGrant, listGrants } from './api/grants'
import type { Grant, GrantListItem, GrantTask, ProblemDetails } from './api/types'
import { AddTaskForm } from './AddTaskForm'
import { CloseGrant } from './CloseGrant'
import { formatCalendarDate } from './dates'
import { OverdueTasks } from './OverdueTasks'
import { ProblemMessage, ServerProblemBanner } from './ProblemMessage'
import { TaskList } from './TaskList'

/**
 * The one screen (CONTEXT §7): choose a grant, see its tasks with overdue ones highlighted, complete and add tasks,
 * and close the grant. "Show overdue tasks" lists the overdue tasks across all grants and can open any of them. Every request goes through `apiFetch`, so each ends in success, an API error or a server
 * problem (§6.3).
 */
export function GrantScreen() {
  const [grants, setGrants] = useState<GrantListItem[] | null>(null)
  const [selectedId, setSelectedId] = useState('')
  const [grant, setGrant] = useState<Grant | null>(null)
  const [serverProblem, setServerProblem] = useState(false)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [completingTaskId, setCompletingTaskId] = useState<string | null>(null)

  // The grant on screen. A response for a grant the officer has since switched away from is ignored.
  const selectedIdRef = useRef('')

  async function loadGrants() {
    const result = await listGrants()
    if (result.kind === 'success') {
      setGrants(result.data)
      if (selectedIdRef.current === '' && result.data.length > 0) {
        await selectGrant(result.data[0].id)
      }
    } else if (result.kind === 'server-problem') {
      setServerProblem(true)
    } else {
      setProblem(result.problem)
    }
  }

  async function loadGrant(grantId: string) {
    const result = await getGrant(grantId)
    if (selectedIdRef.current !== grantId) {
      return
    }

    if (result.kind === 'success') {
      setGrant(result.data)
    } else if (result.kind === 'server-problem') {
      setServerProblem(true)
    } else {
      setProblem(result.problem)
    }
  }

  async function selectGrant(grantId: string) {
    selectedIdRef.current = grantId
    setSelectedId(grantId)
    setGrant(null)
    setProblem(null)
    setServerProblem(false)
    await loadGrant(grantId)
  }

  async function handleComplete(task: GrantTask) {
    setProblem(null)
    setServerProblem(false)
    setCompletingTaskId(task.id)
    const result = await completeTask(task.id)
    setCompletingTaskId(null)

    if (result.kind === 'success') {
      await loadGrant(task.grantId)
    } else if (result.kind === 'server-problem') {
      setServerProblem(true)
    } else {
      setProblem(result.problem)
      // A 409 means the grant changed since the screen loaded (for example it was closed), so show its current state.
      if (result.status === 409) {
        await loadGrant(task.grantId)
      }
    }
  }

  useEffect(() => {
    // Runs once, when the screen opens.
    void loadGrants()
  }, [])

  return (
    <main>
      <header className="page-header">
        <h1>Grant Task Tracker</h1>
        <OverdueTasks onOpenGrant={(grantId) => void selectGrant(grantId)} />
      </header>

      {serverProblem && <ServerProblemBanner />}

      {grants === null ? (
        !serverProblem && <p>Loading grants…</p>
      ) : (
        <div className="field">
          <label htmlFor="grant-select">Grant</label>
          <select id="grant-select" value={selectedId} onChange={(event) => void selectGrant(event.target.value)}>
            {grants.map((item) => (
              <option key={item.id} value={item.id}>
                {item.reference} – {item.organisationName}
              </option>
            ))}
          </select>
        </div>
      )}

      {problem !== null && <ProblemMessage problem={problem} />}

      {grant !== null && (
        <section aria-labelledby="grant-heading">
          <h2 id="grant-heading">
            {grant.reference} – {grant.organisationName}
          </h2>
          <dl className="grant-facts">
            <dt>Status</dt>
            <dd>{grant.status}</dd>
            <dt>Application date</dt>
            <dd>{formatCalendarDate(grant.applicationDate)}</dd>
            <dt>Pending tasks</dt>
            <dd>{grant.pendingTaskCount}</dd>
            {grant.closedNote !== null && (
              <>
                <dt>Closing note</dt>
                <dd>{grant.closedNote}</dd>
              </>
            )}
          </dl>

          <CloseGrant
            key={grant.id}
            grant={grant}
            onClosed={(closed) => {
              if (selectedIdRef.current === closed.id) {
                setGrant(closed)
              }
            }}
            onChanged={() => loadGrant(grant.id)}
          />

          <TaskList grant={grant} completingTaskId={completingTaskId} onComplete={(task) => void handleComplete(task)} />

          <AddTaskForm
            key={grant.id}
            grant={grant}
            onRequestStart={() => {
              setServerProblem(false)
              setProblem(null)
            }}
            onChanged={() => loadGrant(grant.id)}
            onServerProblem={() => setServerProblem(true)}
          />
        </section>
      )}
    </main>
  )
}
