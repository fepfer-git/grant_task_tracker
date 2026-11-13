import type { ProblemDetails } from './api/types'

/** The text OP-3 asks for when a request may not have reached the server (DESIGN.md §6.3). */
export const SERVER_PROBLEM_MESSAGE =
  "We couldn't reach the server. Your change may not have been saved. It's safe to send it again."

/** An API error's `title` and `detail` (DESIGN.md §5.1), for example BR-1 or `grant-closed`. */
export function ProblemMessage({ problem }: { problem: ProblemDetails }) {
  return (
    <div role="alert" className="problem">
      <strong>{problem.title ?? 'The request was rejected.'}</strong>
      {problem.detail !== undefined && <p>{problem.detail}</p>}
    </div>
  )
}

/** The server-problem banner: a 5xx, a network error or a timeout. */
export function ServerProblemBanner() {
  return (
    <div role="alert" className="banner">
      {SERVER_PROBLEM_MESSAGE}
    </div>
  )
}
