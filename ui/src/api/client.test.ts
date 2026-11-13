import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, type ApiResult } from './client'
import type { Grant } from './types'

// fetch is replaced by a mock in every test, and fake timers drive the 10-second timeout, so no test waits.

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

/** A fetch that answers only when its request is aborted, like a server that never responds. */
function fetchThatNeverAnswers() {
  return vi.fn((_input: RequestInfo | URL, init?: RequestInit) =>
    new Promise<Response>((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(new DOMException('The operation was aborted.', 'AbortError')))
    }),
  )
}

/** Records the result when the promise settles, so a test can check it without awaiting a promise that may hang. */
function track<T>(promise: Promise<ApiResult<T>>) {
  const tracked: { result?: ApiResult<T> } = {}
  void promise.then((result) => {
    tracked.result = result
  })
  return tracked
}

describe('test environment', () => {
  it('given the test setup, when reading the time zone, then it is America/Los_Angeles', () => {
    // Arrange: vite.config.ts sets TZ before the test processes start (DESIGN.md §9).
    const marchEleventh = new Date(Date.UTC(2026, 2, 11))

    // Act
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
    const localDay = marchEleventh.getDate()

    // Assert: midnight UTC on 11 March is still 10 March here, which is the mistake the UI tests must catch.
    expect(timeZone).toBe('America/Los_Angeles')
    expect(localDay).toBe(10)
  })
})

describe('apiFetch', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('given a 200 response, when fetching, then returns success with the parsed body', async () => {
    // Arrange
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(200, [{ id: 'g1', reference: 'GR-100001' }])))

    // Act
    const result = await apiFetch('/grants')

    // Assert
    expect(result).toEqual({ kind: 'success', status: 200, data: [{ id: 'g1', reference: 'GR-100001' }] })
  })

  it('given a 400 ProblemDetails with errors, when fetching, then returns an API error with field errors', async () => {
    // Arrange
    const problem = {
      code: 'validation',
      status: 400,
      title: 'One or more validation errors occurred.',
      errors: { dueDate: ['Due date must be today or later.'] },
    }
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(400, problem)))

    // Act
    const result = await apiFetch('/grants/g1/tasks', { method: 'POST', body: { title: 'x' } })

    // Assert
    expect(result).toEqual({ kind: 'api-error', status: 400, problem })
  })

  it('given a 409 ProblemDetails, when fetching, then returns an API error with title and detail', async () => {
    // Arrange
    const problem = {
      code: 'BR-1',
      status: 409,
      title: 'Grant is closed',
      detail: 'Grant GR-100003 is closed, so no tasks can be added.',
    }
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(409, problem)))

    // Act
    const result = await apiFetch('/grants/g3/tasks', { method: 'POST', body: { title: 'x' } })

    // Assert
    expect(result).toEqual({ kind: 'api-error', status: 409, problem })
  })

  it('given a 500 response, when fetching, then returns a server problem', async () => {
    // Arrange
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(500, { code: 'internal', status: 500, title: 'An unexpected error occurred.' })))

    // Act
    const result = await apiFetch('/grants')

    // Assert
    expect(result).toEqual({ kind: 'server-problem', reason: 'server-error' })
  })

  it('given fetch rejects, when fetching, then returns a server problem', async () => {
    // Arrange
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch') }))

    // Act
    const result = await apiFetch('/grants')

    // Assert
    expect(result).toEqual({ kind: 'server-problem', reason: 'network' })
  })

  it('given no response within 10 seconds, when fetching, then aborts and returns a server problem', async () => {
    // Arrange
    const fetchMock = fetchThatNeverAnswers()
    vi.stubGlobal('fetch', fetchMock)
    const tracked = track(apiFetch('/grants'))

    // Act
    await vi.advanceTimersByTimeAsync(10_000)

    // Assert
    expect(tracked.result).toEqual({ kind: 'server-problem', reason: 'timeout' })
    expect(fetchMock.mock.calls[0][1]?.signal?.aborted).toBe(true)
  })

  it('given a response just before 10 seconds, when fetching, then returns success', async () => {
    // Arrange: the response arrives at 9,999 ms.
    vi.stubGlobal('fetch', vi.fn((_input: RequestInfo | URL, init?: RequestInit) =>
      new Promise<Response>((resolve, reject) => {
        init?.signal?.addEventListener('abort', () => reject(new DOMException('The operation was aborted.', 'AbortError')))
        setTimeout(() => resolve(jsonResponse(200, [])), 9_999)
      }),
    ))
    const tracked = track(apiFetch('/grants'))

    // Act
    await vi.advanceTimersByTimeAsync(9_999)

    // Assert
    expect(tracked.result).toEqual({ kind: 'success', status: 200, data: [] })
  })

  it('given a grant response, when fetching, then dueDate and today stay as the exact strings sent', async () => {
    // Arrange: the API sends calendar dates as "YYYY-MM-DD". The tests run in America/Los_Angeles, where a Date made
    // from "2026-03-11" is 10 March.
    const grant = {
      id: 'g1',
      applicationDate: '2025-09-14',
      today: '2026-03-11',
      tasks: [{ id: 't1', dueDate: '2026-03-11', createdAtUtc: '2026-03-10T22:00:00Z' }],
    }
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(200, grant)))

    // Act
    const result = await apiFetch<Grant>('/grants/g1')

    // Assert
    expect(result.kind).toBe('success')
    const data = (result as Extract<ApiResult<Grant>, { kind: 'success' }>).data
    expect(data.today).toBe('2026-03-11')
    expect(data.applicationDate).toBe('2025-09-14')
    expect(data.tasks[0].dueDate).toBe('2026-03-11')
  })
})
