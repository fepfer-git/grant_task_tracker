import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { GrantScreen } from './GrantScreen'
import { SERVER_PROBLEM_MESSAGE } from './ProblemMessage'
import { json, makeGrant, makeTask, mockApi } from './test/mockApi'
import type { Grant } from './api/types'

/** Renders the screen and waits until the first grant is shown. */
async function renderScreen(grant: Grant) {
  render(<GrantScreen />)
  await screen.findByRole('heading', { level: 2, name: `${grant.reference} – ${grant.organisationName}` })
}

function taskRow(title: string) {
  return screen.getByRole('row', { name: new RegExp(title) })
}

describe('GrantScreen', () => {
  it('given a grant with an overdue task, when the screen loads, then the task is highlighted as overdue', async () => {
    // Arrange
    const grant = makeGrant({
      tasks: [
        makeTask({ id: 't1', title: 'Verify bank details', dueDate: '2026-03-08', isOverdue: true }),
        makeTask({ id: 't2', title: 'Request quote for tools', dueDate: '2026-03-16', isOverdue: false }),
      ],
    })
    mockApi([grant])

    // Act
    await renderScreen(grant)

    // Assert: highlighted by colour and by a label, so it doesn't rely on colour alone.
    expect(taskRow('Verify bank details')).toHaveClass('overdue')
    expect(within(taskRow('Verify bank details')).getByText('Overdue')).toBeInTheDocument()
    expect(taskRow('Request quote for tools')).not.toHaveClass('overdue')
    expect(within(taskRow('Request quote for tools')).queryByText('Overdue')).not.toBeInTheDocument()
  })

  it('given a task due "2026-03-11", when the screen loads, then the due date shown is 11 March 2026', async () => {
    // Arrange: the tests run in America/Los_Angeles, where a Date made from "2026-03-11" is 10 March.
    const grant = makeGrant({ tasks: [makeTask({ title: 'Verify bank details', dueDate: '2026-03-11' })] })
    mockApi([grant])

    // Act
    await renderScreen(grant)

    // Assert
    expect(within(taskRow('Verify bank details')).getByText('11 March 2026')).toBeInTheDocument()
  })

  it('given pending and completed tasks on an open grant, when the screen loads, then only pending tasks have a Complete button', async () => {
    // Arrange
    const grant = makeGrant({
      tasks: [
        makeTask({ id: 't1', title: 'Request quote for tools', status: 'Pending' }),
        makeTask({ id: 't2', title: 'Check eligibility documents', status: 'Completed', completedAtUtc: '2026-01-20T22:00:00Z' }),
      ],
    })
    mockApi([grant])

    // Act
    await renderScreen(grant)

    // Assert
    const buttons = screen.getAllByRole('button', { name: /^Complete / })
    expect(buttons.map((button) => button.getAttribute('aria-label'))).toEqual(['Complete Request quote for tools'])
  })

  it('given a closed grant, when the screen loads, then the Add task form is disabled and there are no Complete buttons', async () => {
    // Arrange: a closed grant can't really have a pending task (closing cancels them). One is included anyway, so
    // the test checks the UI's own rule: no Complete button on a closed grant, whatever the task's status.
    const grant = makeGrant({
      status: 'Closed',
      closedAtUtc: '2026-03-01T22:00:00Z',
      tasks: [
        makeTask({ id: 't1', title: 'Final acquittal review', status: 'Completed', completedAtUtc: '2026-02-01T22:00:00Z' }),
        makeTask({ id: 't2', title: 'Schedule site visit', status: 'Cancelled', cancelledAtUtc: '2026-03-01T22:00:00Z' }),
        makeTask({ id: 't3', title: 'Review progress report', status: 'Pending' }),
      ],
    })
    mockApi([grant])

    // Act
    await renderScreen(grant)

    // Assert
    expect(screen.queryAllByRole('button', { name: /^Complete / })).toEqual([])
    expect(screen.getByLabelText('Title')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Add task' })).toBeDisabled()
  })

  it('given the API returns 500, when completing a task, then the banner is shown', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant({ tasks: [makeTask({ id: 't1', title: 'Verify bank details' })] })
    mockApi([grant], (request) =>
      request.method === 'PUT' && request.path === '/tasks/t1/complete'
        ? json(500, { code: 'internal', status: 500, title: 'An unexpected error occurred.' })
        : json(404, {}),
    )
    await renderScreen(grant)

    // Act
    await user.click(screen.getByRole('button', { name: 'Complete Verify bank details' }))

    // Assert
    expect(await screen.findByText(SERVER_PROBLEM_MESSAGE)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Complete Verify bank details' })).toBeEnabled()
  })
})
