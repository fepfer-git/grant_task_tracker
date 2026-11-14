import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { Grant, OverdueTask } from './api/types'
import { GrantScreen } from './GrantScreen'
import { SERVER_PROBLEM_MESSAGE } from './ProblemMessage'
import { json, makeGrant, makeTask, mockApi } from './test/mockApi'

// The overdue tasks dialog is tested through the whole screen, as the officer uses it, with the API replaced by mockApi.

const OVERDUE_PATH = '/tasks/overdue'

const riverside = makeGrant({
  id: 'grant-1',
  reference: 'GR-100001',
  organisationName: 'Riverside Community Garden Inc.',
  pendingTaskCount: 1,
  tasks: [makeTask({ id: 't1', grantId: 'grant-1', title: 'Verify bank details', dueDate: '2026-03-08', priority: 'High', isOverdue: true })],
})

const northside = makeGrant({
  id: 'grant-2',
  reference: 'GR-100002',
  organisationName: 'Northside Youth Sports Club',
  pendingTaskCount: 1,
  tasks: [makeTask({ id: 't4', grantId: 'grant-2', title: 'Review progress report', dueDate: '2026-03-10', priority: 'Low', isOverdue: true })],
})

/** The two seed-like overdue tasks, most overdue first, as the API sends them. */
const overdueTasks: OverdueTask[] = [
  {
    id: 't1',
    grantId: 'grant-1',
    grantReference: 'GR-100001',
    organisationName: 'Riverside Community Garden Inc.',
    title: 'Verify bank details',
    dueDate: '2026-03-08',
    priority: 'High',
  },
  {
    id: 't4',
    grantId: 'grant-2',
    grantReference: 'GR-100002',
    organisationName: 'Northside Youth Sports Club',
    title: 'Review progress report',
    dueDate: '2026-03-10',
    priority: 'Low',
  },
]

async function renderScreen(grant: Grant) {
  render(<GrantScreen />)
  await screen.findByRole('heading', { level: 2, name: `${grant.reference} – ${grant.organisationName}` })
}

function showButton() {
  return screen.getByRole('button', { name: 'Show overdue tasks' })
}

function dialog() {
  return screen.getByRole('dialog', { name: 'Overdue tasks' })
}

describe('OverdueTasks', () => {
  it('given overdue tasks on two grants, when showing overdue tasks, then they are listed in the API order with grant, task, due date and priority', async () => {
    // Arrange
    const user = userEvent.setup()
    mockApi([riverside, northside], (request) => (request.path === OVERDUE_PATH ? json(200, overdueTasks) : json(404, {})))
    await renderScreen(riverside)

    // Act
    await user.click(showButton())

    // Assert: the due dates are built from the text, so they're right in the tests' America/Los_Angeles time zone.
    const rows = await within(dialog()).findAllByRole('row')
    expect(rows.map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent))).toEqual([
      [],
      ['GR-100001 – Riverside Community Garden Inc.', 'Verify bank details', '8 March 2026', 'High', 'Open grant'],
      ['GR-100002 – Northside Youth Sports Club', 'Review progress report', '10 March 2026', 'Low', 'Open grant'],
    ])
  })

  it('given no overdue tasks, when showing overdue tasks, then the dialog says there are none', async () => {
    // Arrange
    const user = userEvent.setup()
    mockApi([riverside], (request) => (request.path === OVERDUE_PATH ? json(200, []) : json(404, {})))
    await renderScreen(riverside)

    // Act
    await user.click(showButton())

    // Assert
    expect(await within(dialog()).findByText('There are no overdue tasks.')).toBeInTheDocument()
    expect(within(dialog()).queryByRole('table')).not.toBeInTheDocument()
  })

  it('given the API returns 500, when showing overdue tasks, then the banner is shown in the dialog', async () => {
    // Arrange
    const user = userEvent.setup()
    mockApi([riverside], (request) =>
      request.path === OVERDUE_PATH ? json(500, { code: 'internal', status: 500, title: 'An unexpected error occurred.' }) : json(404, {}),
    )
    await renderScreen(riverside)

    // Act
    await user.click(showButton())

    // Assert
    expect(await within(dialog()).findByText(SERVER_PROBLEM_MESSAGE)).toBeInTheDocument()
    expect(within(dialog()).getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })

  it('given the first load failed, when trying again, then the overdue tasks are listed', async () => {
    // Arrange: the first request fails, the second succeeds.
    const user = userEvent.setup()
    let calls = 0
    const { sent } = mockApi([riverside, northside], (request) => {
      if (request.path !== OVERDUE_PATH) {
        return json(404, {})
      }
      calls++
      return calls === 1 ? json(503, {}) : json(200, overdueTasks)
    })
    await renderScreen(riverside)
    await user.click(showButton())
    await within(dialog()).findByText(SERVER_PROBLEM_MESSAGE)

    // Act
    await user.click(within(dialog()).getByRole('button', { name: 'Try again' }))

    // Assert
    expect(await within(dialog()).findByRole('row', { name: /Review progress report/ })).toBeInTheDocument()
    expect(within(dialog()).queryByText(SERVER_PROBLEM_MESSAGE)).not.toBeInTheDocument()
    expect(sent('GET', OVERDUE_PATH)).toHaveLength(2)
  })

  it('given an overdue task on another grant, when selecting Open grant, then the dialog closes and that grant is shown', async () => {
    // Arrange
    const user = userEvent.setup()
    mockApi([riverside, northside], (request) => (request.path === OVERDUE_PATH ? json(200, overdueTasks) : json(404, {})))
    await renderScreen(riverside)
    await user.click(showButton())

    // Act
    await user.click(await within(dialog()).findByRole('button', { name: 'Open grant for Review progress report' }))

    // Assert
    expect(await screen.findByRole('heading', { level: 2, name: 'GR-100002 – Northside Youth Sports Club' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog', { name: 'Overdue tasks' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Grant')).toHaveValue('grant-2')
    expect(screen.getByRole('row', { name: /Review progress report/ })).toHaveClass('overdue')
  })

  it.each([
    ['pressing Escape', async (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}')],
    ['selecting Close', async (user: ReturnType<typeof userEvent.setup>) => user.click(within(dialog()).getByRole('button', { name: 'Close' }))],
  ])('given the dialog is open, when %s, then the dialog closes and focus returns to the button', async (_action, closeIt) => {
    // Arrange
    const user = userEvent.setup()
    mockApi([riverside], (request) => (request.path === OVERDUE_PATH ? json(200, overdueTasks) : json(404, {})))
    await renderScreen(riverside)
    await user.click(showButton())
    await within(dialog()).findAllByRole('row')

    // Act
    await closeIt(user)

    // Assert
    expect(screen.queryByRole('dialog', { name: 'Overdue tasks' })).not.toBeInTheDocument()
    expect(showButton()).toHaveFocus()
  })

  it('given the first load is still waiting, when the dialog is closed and shown again, then the older response is ignored', async () => {
    // Arrange: the first request answers only when the test says so, after the second one has answered.
    const user = userEvent.setup()
    let answerFirst: (response: Response) => void = () => {}
    let calls = 0
    const { sent } = mockApi([riverside, northside], (request) => {
      if (request.path !== OVERDUE_PATH) {
        return json(404, {})
      }
      calls++
      return calls === 1 ? new Promise<Response>((resolve) => (answerFirst = resolve)) : json(200, [])
    })
    await renderScreen(riverside)
    await user.click(showButton())
    await user.keyboard('{Escape}')
    await user.click(showButton())
    await within(dialog()).findByText('There are no overdue tasks.')

    // Act: answer, then let the response be read and any state update render.
    await act(async () => {
      answerFirst(json(200, overdueTasks))
      await new Promise((resolve) => setTimeout(resolve, 0))
      await new Promise((resolve) => setTimeout(resolve, 0))
    })

    // Assert: the dialog keeps the newer, empty list.
    expect(sent('GET', OVERDUE_PATH)).toHaveLength(2)
    expect(within(dialog()).getByText('There are no overdue tasks.')).toBeInTheDocument()
    expect(within(dialog()).queryByRole('table')).not.toBeInTheDocument()
  })

  it('given a task was completed after the list was shown, when showing overdue tasks again, then the list is loaded again', async () => {
    // Arrange: the API's list changes between the two openings, as it would after a task is completed.
    const user = userEvent.setup()
    let current = overdueTasks
    const { sent } = mockApi([riverside, northside], (request) => (request.path === OVERDUE_PATH ? json(200, current) : json(404, {})))
    await renderScreen(riverside)
    await user.click(showButton())
    await within(dialog()).findByRole('row', { name: /Verify bank details/ })
    await user.keyboard('{Escape}')
    current = overdueTasks.slice(1)

    // Act
    await user.click(showButton())

    // Assert
    expect(await within(dialog()).findByRole('row', { name: /Review progress report/ })).toBeInTheDocument()
    expect(within(dialog()).queryByRole('row', { name: /Verify bank details/ })).not.toBeInTheDocument()
    expect(sent('GET', OVERDUE_PATH)).toHaveLength(2)
  })
})
