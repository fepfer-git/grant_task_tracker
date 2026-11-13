import { useState, type FormEvent, type KeyboardEvent } from 'react'
import { closeGrant } from './api/grants'
import type { Grant, ProblemDetails } from './api/types'
import { ProblemMessage, ServerProblemBanner } from './ProblemMessage'

/**
 * Why Close can't be used now, or null when it can (DESIGN.md BR-5 UI, §4.7). The pending High tasks come from the
 * grant on screen. The API checks BR-5 again when the close is sent.
 */
export function closeBlockedReason(grant: Grant): string | null {
  if (grant.status === 'Closed') {
    return 'This grant is already closed.'
  }

  const pendingHigh = grant.tasks.filter((task) => task.status === 'Pending' && task.priority === 'High').length
  if (pendingHigh === 1) {
    return 'This grant has 1 pending High-priority task. Complete it before closing the grant.'
  }
  if (pendingHigh > 1) {
    return `This grant has ${pendingHigh} pending High-priority tasks. Complete them before closing the grant.`
  }

  return null
}

/** The confirmation question. Pending tasks are cancelled by the close, so the officer is told how many. */
export function closeQuestion(grant: Grant): string {
  const pending = grant.pendingTaskCount
  if (pending === 0) {
    return `Close grant ${grant.reference}?`
  }
  if (pending === 1) {
    return 'There is still 1 pending task. Closing the grant will cancel it. Do you still want to close the grant?'
  }
  return `There are still ${pending} pending tasks. Closing the grant will cancel them. Do you still want to close the grant?`
}

interface CloseGrantProps {
  grant: Grant
  /** The API's closed grant, shown in place of the old one. */
  onClosed: (grant: Grant) => void
  /** Reload the grant after a 409: it changed since the screen loaded. */
  onChanged: () => Promise<void>
}

/**
 * The Close button and its confirmation dialog (DESIGN.md BR-5 UI). Render it with `key={grant.id}`, so switching
 * grants closes the dialog and clears the note.
 */
export function CloseGrant({ grant, onClosed, onChanged }: CloseGrantProps) {
  const [open, setOpen] = useState(false)
  const [note, setNote] = useState('')
  const [noteErrors, setNoteErrors] = useState<string[] | null>(null)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [serverProblem, setServerProblem] = useState(false)
  const [sending, setSending] = useState(false)

  const blockedReason = closeBlockedReason(grant)

  function openDialog() {
    setOpen(true)
    setProblem(null)
    setServerProblem(false)
    setNoteErrors(null)
  }

  function cancel() {
    if (!sending) {
      setOpen(false)
    }
  }

  async function handleConfirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setProblem(null)
    setServerProblem(false)
    setNoteErrors(null)

    setSending(true)
    const trimmed = note.trim()
    const result = await closeGrant(grant.id, trimmed === '' ? {} : { note: trimmed })
    setSending(false)

    switch (result.kind) {
      case 'success':
        setOpen(false)
        setNote('')
        onClosed(result.data)
        return

      case 'api-error':
        if (result.problem.errors?.note !== undefined) {
          setNoteErrors(result.problem.errors.note)
        } else {
          setProblem(result.problem)
        }
        // A 409 means the grant changed since the screen loaded (for example, a High task was added), so show its
        // current state. The dialog stays open with the message.
        if (result.status === 409) {
          await onChanged()
        }
        return

      case 'server-problem':
        // The note is kept, and closing again is safe: a repeated close changes nothing (§6.3).
        setServerProblem(true)
        return
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      cancel()
    }
  }

  return (
    <div className="close-grant">
      <button
        type="button"
        onClick={openDialog}
        disabled={blockedReason !== null || open}
        title={blockedReason ?? undefined}
        aria-describedby={blockedReason === null ? undefined : 'close-blocked-reason'}
      >
        Close grant
      </button>
      {blockedReason !== null && (
        <p id="close-blocked-reason" className="hint">
          {blockedReason}
        </p>
      )}

      {open && (
        <div role="dialog" aria-modal="true" aria-labelledby="close-dialog-question" className="dialog" onKeyDown={handleKeyDown}>
          <form onSubmit={handleConfirm} noValidate>
            <p id="close-dialog-question">{closeQuestion(grant)}</p>

            <fieldset disabled={sending}>
              <legend className="visually-hidden">Close grant</legend>
              <div className="field">
                <label htmlFor="close-note">Note (optional)</label>
                <textarea
                  id="close-note"
                  value={note}
                  rows={3}
                  autoFocus
                  onChange={(event) => setNote(event.target.value)}
                  aria-invalid={noteErrors !== null}
                  aria-describedby={noteErrors === null ? undefined : 'close-note-error'}
                />
                {noteErrors !== null && (
                  <p id="close-note-error" className="field-error">
                    {noteErrors.join(' ')}
                  </p>
                )}
              </div>

              <div className="dialog-buttons">
                <button type="submit" disabled={blockedReason !== null}>
                  {sending ? 'Closing…' : 'Yes, close the grant'}
                </button>
                <button type="button" onClick={cancel}>
                  Cancel
                </button>
              </div>
            </fieldset>

            {problem !== null && <ProblemMessage problem={problem} />}
            {serverProblem && <ServerProblemBanner />}
          </form>
        </div>
      )}
    </div>
  )
}
