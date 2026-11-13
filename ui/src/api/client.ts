import type { ProblemDetails } from './types'

/** How long a request may take before it's treated as a server problem (DESIGN.md §6.3: slow regional links). */
export const REQUEST_TIMEOUT_MS = 10_000

/**
 * Every request ends in exactly one of these (DESIGN.md §6.3):
 * - `success`: a 2xx, with the parsed body.
 * - `api-error`: a 4xx. The server rejected the request and said why (field errors, or a title and detail).
 * - `server-problem`: a 5xx, a network error, or no response in time. The change may or may not have been saved,
 *   and sending it again is safe.
 */
export type ApiResult<T> =
  | { kind: 'success'; status: number; data: T }
  | { kind: 'api-error'; status: number; problem: ProblemDetails }
  | { kind: 'server-problem'; reason: 'server-error' | 'network' | 'timeout' }

export interface ApiRequest {
  method?: 'GET' | 'POST' | 'PUT'
  /** Sent as JSON. */
  body?: unknown
}

/**
 * The one fetch helper (DESIGN.md §6.3). It never throws: every outcome is an {@link ApiResult}. The JSON is parsed
 * as it is, so calendar dates stay "YYYY-MM-DD" strings.
 */
export async function apiFetch<T>(path: string, request: ApiRequest = {}): Promise<ApiResult<T>> {
  const controller = new AbortController()
  let timedOut = false
  const timer = setTimeout(() => {
    timedOut = true
    controller.abort()
  }, REQUEST_TIMEOUT_MS)

  try {
    const response = await fetch(path, {
      method: request.method ?? 'GET',
      headers:
        request.body === undefined
          ? { Accept: 'application/json' }
          : { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: request.body === undefined ? undefined : JSON.stringify(request.body),
      signal: controller.signal,
    })

    // The body is read inside the timeout too: a response that starts but never finishes is still no response.
    const text = await response.text()

    if (response.ok) {
      return { kind: 'success', status: response.status, data: (text === '' ? undefined : JSON.parse(text)) as T }
    }

    if (response.status >= 400 && response.status < 500) {
      return { kind: 'api-error', status: response.status, problem: readProblem(text, response) }
    }

    return { kind: 'server-problem', reason: 'server-error' }
  } catch {
    // The request was aborted by the timer, fetch failed, or the body couldn't be read or parsed.
    return { kind: 'server-problem', reason: timedOut ? 'timeout' : 'network' }
  } finally {
    clearTimeout(timer)
  }
}

/** The 4xx body as ProblemDetails. A body that isn't JSON (rare, from a proxy) still gives a status and a title. */
function readProblem(text: string, response: Response): ProblemDetails {
  try {
    const parsed: unknown = JSON.parse(text)
    if (parsed !== null && typeof parsed === 'object') {
      return parsed as ProblemDetails
    }
  } catch {
    // Not JSON: fall through to the generic problem below.
  }

  return { status: response.status, title: response.statusText || 'The request was rejected.' }
}
