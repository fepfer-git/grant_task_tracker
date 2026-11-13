import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Grant } from './api/types'
import { GrantScreen } from './GrantScreen'
import { SERVER_PROBLEM_MESSAGE } from './ProblemMessage'
import { json, makeGrant, makeTask, mockApi, noResponse } from './test/mockApi'

// Closing is tested through the whole screen, as the officer uses it, with the API replaced by mockApi.

const CLOSE_PATH = '/grants/grant-1/close'

async function renderScreen(grant: Grant) {
  render(<GrantScreen />)
  await screen.findByRole('heading', { level: 2, name: `${grant.reference} – ${grant.organisationName}` })
}

function closeButton() {
  return screen.getByRole('button', { name: 'Close grant' })
}

/** The grant as the API returns it after closing: Closed, with its pending tasks cancelled. */
function closedVersionOf(grant: Grant, note: string | null): Grant {
  return {
    ...grant,
    status: 'Closed',
    closedAtUtc: '2026-03-10T22:00:00Z',
    closedNote: note,
    pendingTaskCount: 0,
    tasks: grant.tasks.map((task) =>
      task.status === 'Pending' ? { ...task, status: 'Cancelled', cancelledAtUtc: '2026-03-10T22:00:00Z' } : task,
    ),
  }
}

describe('CloseGrant', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('given a pending High task, when the screen loads, then Close is disabled with the reason', async () => {
    // Arrange
    const grant = makeGrant({
      pendingTaskCount: 2,
      tasks: [
        makeTask({ id: 't1', title: 'Verify bank details', priority: 'High', status: 'Pending' }),
        makeTask({ id: 't2', title: 'Request quote for tools', priority: 'Medium', status: 'Pending' }),
      ],
    })
    mockApi([grant])

    // Act
    await renderScreen(grant)

    // Assert: the reason is the button's description (screen readers) and its tooltip.
    const reason = 'This grant has 1 pending High-priority task. Complete it before closing the grant.'
    expect(closeButton()).toBeDisabled()
    expect(closeButton()).toHaveAccessibleDescription(reason)
    expect(closeButton()).toHaveAttribute('title', reason)
  })

  it('given a closed grant, when the screen loads, then Close is disabled', async () => {
    // Arrange
    const grant = makeGrant({ status: 'Closed', closedAtUtc: '2026-03-01T22:00:00Z' })
    mockApi([grant])

    // Act
    await renderScreen(grant)

    // Assert
    expect(closeButton()).toBeDisabled()
    expect(closeButton()).toHaveAccessibleDescription('This grant is already closed.')
  })

  it.each([
    [1, 'There is still 1 pending task. Closing the grant will cancel it. Do you still want to close the grant?'],
    [2, 'There are still 2 pending tasks. Closing the grant will cancel them. Do you still want to close the grant?'],
  ])('given %i pending task(s), when clicking Close, then the dialog says they will be cancelled', async (pending, question) => {
    // Arrange: Low and Medium tasks only, so BR-5 doesn't block the close.
    const user = userEvent.setup()
    const tasks = [
      makeTask({ id: 't1', title: 'Review progress report', priority: 'Low' }),
      makeTask({ id: 't2', title: 'Schedule site visit', priority: 'Medium' }),
    ].slice(0, pending)
    const grant = makeGrant({ pendingTaskCount: pending, tasks })
    mockApi([grant])
    await renderScreen(grant)

    // Act
    await user.click(closeButton())

    // Assert
    expect(screen.getByRole('dialog', { name: question })).toBeInTheDocument()
  })

  it('given no pending tasks, when clicking Close, then the dialog asks for a plain confirmation', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant({ reference: 'GR-100004', organisationName: 'Hillview Library Friends Group' })
    mockApi([grant])
    await renderScreen(grant)

    // Act
    await user.click(closeButton())

    // Assert
    expect(screen.getByRole('dialog', { name: 'Close grant GR-100004?' })).toBeInTheDocument()
  })

  it('given the API returns 409 BR-5, when confirming, then its title and detail are shown and the grant reloads', async () => {
    // Arrange: another officer added a High task after this screen loaded. The API now has it.
    const user = userEvent.setup()
    const grant = makeGrant()
    const grants = [grant]
    const { sent } = mockApi(grants, () => {
      grants[0] = {
        ...grant,
        pendingTaskCount: 1,
        tasks: [makeTask({ id: 't9', title: 'Verify bank details', priority: 'High' })],
      }
      return json(409, {
        code: 'BR-5',
        status: 409,
        title: 'Grant has pending High-priority tasks',
        detail: "A grant can't be closed while it has pending High-priority tasks.",
        blockingTaskIds: ['t9'],
      })
    })
    await renderScreen(grant)
    await user.click(closeButton())

    // Act
    await user.click(screen.getByRole('button', { name: 'Yes, close the grant' }))

    // Assert
    expect(await screen.findByText('Grant has pending High-priority tasks')).toBeInTheDocument()
    expect(screen.getByText("A grant can't be closed while it has pending High-priority tasks.")).toBeInTheDocument()
    expect(await screen.findByRole('row', { name: /Verify bank details/ })).toBeInTheDocument()
    expect(sent('GET', '/grants/grant-1')).toHaveLength(2)
  })

  it('given a note, when confirming, then the note is sent and the closed grant is shown', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant({ pendingTaskCount: 1, tasks: [makeTask({ id: 't1', title: 'Schedule site visit', priority: 'Medium' })] })
    const { sent } = mockApi([grant], () => json(200, closedVersionOf(grant, 'Acquittal complete.')))
    await renderScreen(grant)
    await user.click(closeButton())
    await user.type(screen.getByLabelText('Note (optional)'), '  Acquittal complete.  ')

    // Act
    await user.click(screen.getByRole('button', { name: 'Yes, close the grant' }))

    // Assert
    expect(await screen.findByText('Acquittal complete.')).toBeInTheDocument()
    expect(sent('PUT', CLOSE_PATH).map((request) => request.body)).toEqual([{ note: 'Acquittal complete.' }])
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('row', { name: /Schedule site visit/ })).toHaveTextContent('Cancelled')
    expect(closeButton()).toBeDisabled()
  })

  it('given the API returns 400 keyed on note, when confirming, then the message is shown on the note field', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant()
    mockApi([grant], () =>
      json(400, {
        code: 'validation',
        status: 400,
        title: 'One or more validation errors occurred.',
        errors: { note: ['Note must be 1000 characters or fewer.'] },
      }),
    )
    await renderScreen(grant)
    await user.click(closeButton())
    await user.click(screen.getByLabelText('Note (optional)'))
    await user.paste('x'.repeat(1001))

    // Act
    await user.click(screen.getByRole('button', { name: 'Yes, close the grant' }))

    // Assert
    await vi.waitFor(() =>
      expect(screen.getByLabelText('Note (optional)')).toHaveAccessibleDescription('Note must be 1000 characters or fewer.'),
    )
  })

  it('given no response within 10 seconds, when confirming, then the banner is shown, the note is kept and the button is enabled again', async () => {
    // Arrange
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const grant = makeGrant()
    mockApi([grant], (request) => noResponse(request.signal))
    await renderScreen(grant)
    await user.click(closeButton())
    await user.type(screen.getByLabelText('Note (optional)'), 'Acquittal complete.')
    await user.click(screen.getByRole('button', { name: 'Yes, close the grant' }))

    // Act
    await act(() => vi.advanceTimersByTimeAsync(10_000))

    // Assert
    expect(screen.getByText(SERVER_PROBLEM_MESSAGE)).toBeInTheDocument()
    expect(screen.getByLabelText('Note (optional)')).toHaveValue('Acquittal complete.')
    expect(screen.getByRole('button', { name: 'Yes, close the grant' })).toBeEnabled()
  })

  it('given the dialog is open, when cancelling, then the dialog closes and no request is sent', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant()
    const { sent } = mockApi([grant], () => json(200, closedVersionOf(grant, null)))
    await renderScreen(grant)
    await user.click(closeButton())

    // Act
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    // Assert
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sent('PUT', CLOSE_PATH)).toEqual([])
    expect(closeButton()).toBeEnabled()
  })
})
