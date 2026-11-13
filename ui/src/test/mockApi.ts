// A fake API for the UI tests: `fetch` is replaced, so no test needs a server. Each test builds its own data, so
// tests don't share state.
import { vi } from 'vitest'
import type { Grant, GrantListItem, GrantTask } from '../api/types'

/** The API's "today" in the tests: the same Sydney date as the .NET tests (DESIGN.md §9). */
export const TODAY = '2026-03-11'

export function makeTask(overrides: Partial<GrantTask> = {}): GrantTask {
  return {
    id: 'task-1',
    grantId: 'grant-1',
    title: 'Verify bank details',
    dueDate: '2026-03-20',
    priority: 'Medium',
    status: 'Pending',
    isOverdue: false,
    createdAtUtc: '2026-01-10T22:00:00Z',
    completedAtUtc: null,
    cancelledAtUtc: null,
    ...overrides,
  }
}

export function makeGrant(overrides: Partial<Grant> = {}): Grant {
  return {
    id: 'grant-1',
    reference: 'GR-100001',
    organisationName: 'Riverside Community Garden Inc.',
    applicationDate: '2026-01-10',
    status: 'Open',
    closedAtUtc: null,
    pendingTaskCount: 0,
    closedNote: null,
    today: TODAY,
    tasks: [],
    ...overrides,
  }
}

function listItem(grant: Grant): GrantListItem {
  return {
    id: grant.id,
    reference: grant.reference,
    organisationName: grant.organisationName,
    applicationDate: grant.applicationDate,
    status: grant.status,
    closedAtUtc: grant.closedAtUtc,
    pendingTaskCount: grant.pendingTaskCount,
  }
}

export function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

/** A response that never arrives: the promise rejects only when the request is aborted (the 10-second timeout). */
export function noResponse(signal: AbortSignal | null | undefined): Promise<Response> {
  return new Promise((_resolve, reject) => {
    signal?.addEventListener('abort', () => reject(new DOMException('The operation was aborted.', 'AbortError')))
  })
}

export interface ApiRequest {
  method: string
  path: string
  body: unknown
  signal: AbortSignal | null | undefined
}

type Handler = (request: ApiRequest) => Response | Promise<Response>

/**
 * Replaces `fetch`. `GET /grants` and `GET /grants/{id}` are answered from `grants`, whose entries a handler may
 * change (for example after a successful create). Any other request goes to `handle`. Every request is recorded.
 */
export function mockApi(grants: Grant[], handle: Handler = () => json(500, {})) {
  const requests: ApiRequest[] = []

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request: ApiRequest = {
      method: init?.method ?? 'GET',
      path: String(input),
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
      signal: init?.signal,
    }
    requests.push(request)

    if (request.method === 'GET' && request.path === '/grants') {
      return json(200, grants.map(listItem))
    }

    const grantMatch = /^\/grants\/([^/]+)$/.exec(request.path)
    if (request.method === 'GET' && grantMatch !== null) {
      const grant = grants.find((g) => g.id === decodeURIComponent(grantMatch[1]))
      return grant === undefined ? json(404, { code: 'not-found', title: 'Grant not found' }) : json(200, grant)
    }

    return handle(request)
  })

  vi.stubGlobal('fetch', fetchMock)

  return {
    requests,
    /** The requests sent with this method to this path, for example `sent('POST', '/grants/grant-1/tasks')`. */
    sent: (method: string, path: string) => requests.filter((r) => r.method === method && r.path === path),
  }
}
