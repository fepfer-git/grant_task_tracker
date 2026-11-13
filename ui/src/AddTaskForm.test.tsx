import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { CreateTaskRequest, Grant } from './api/types'
import { GrantScreen } from './GrantScreen'
import { SERVER_PROBLEM_MESSAGE } from './ProblemMessage'
import { json, makeGrant, makeTask, mockApi, noResponse, TODAY } from './test/mockApi'

// The form is tested through the whole screen, as the officer uses it, with the API replaced by mockApi.

const TASKS_PATH = '/grants/grant-1/tasks'

async function renderScreen(grant: Grant) {
  render(<GrantScreen />)
  await screen.findByRole('heading', { level: 2, name: `${grant.reference} – ${grant.organisationName}` })
}

/** Fills in the form. The date input gets its value directly, as a browser's date picker sets it. */
async function fillForm(user: ReturnType<typeof userEvent.setup>, values: { title: string; dueDate: string }) {
  const title = screen.getByLabelText('Title')
  await user.clear(title)
  if (values.title !== '') {
    await user.click(title)
    await user.paste(values.title)
  }
  fireEvent.change(screen.getByLabelText('Due date'), { target: { value: values.dueDate } })
}

function sentTasks(sent: ReturnType<typeof mockApi>['sent']) {
  return sent('POST', TASKS_PATH).map((request) => request.body as CreateTaskRequest)
}

/** A 201 with the task the request asked for. */
function created(body: unknown) {
  const request = body as CreateTaskRequest
  return json(201, makeTask({ id: request.id, title: request.title, dueDate: request.dueDate, priority: request.priority }))
}

describe('AddTaskForm', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it.each([
    ['an empty title', ''],
    ['a whitespace-only title', '   '],
    ['a 101-character title', 'x'.repeat(101)],
  ])('given %s, when submitting, then shows a title error and sends no request', async (_case, title) => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant()
    const { sent } = mockApi([grant], (request) => created(request.body))
    await renderScreen(grant)
    await fillForm(user, { title, dueDate: '2026-03-20' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription(
      title.trim() === '' ? 'Title is required.' : 'Title must be 100 characters or fewer.',
    )
    expect(sentTasks(sent)).toEqual([])
  })

  it('given a 100-character title, when submitting, then the request is sent', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant()
    const { sent } = mockApi([grant], (request) => created(request.body))
    await renderScreen(grant)
    await fillForm(user, { title: 'x'.repeat(100), dueDate: '2026-03-20' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await screen.findByRole('status')
    expect(sentTasks(sent).map((task) => task.title)).toEqual(['x'.repeat(100)])
  })

  it.each([
    ['before today', { applicationDate: '2026-01-10' }, '2026-03-10', 'Due date cannot be in the past.'],
    // An application date after today isn't realistic, but it's the only way to test this check on its own.
    ['before the application date', { applicationDate: '2026-03-20' }, '2026-03-15', "Due date cannot be before the grant's application date."],
  ])('given a due date %s, when submitting, then shows a due date error and sends no request', async (_case, grantValues, dueDate, message) => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant(grantValues)
    const { sent } = mockApi([grant], (request) => created(request.body))
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    expect(screen.getByLabelText('Due date')).toHaveAccessibleDescription(message)
    expect(sentTasks(sent)).toEqual([])
  })

  it('given a due date equal to today, when submitting, then the request is sent', async () => {
    // Arrange: "today" is the API's (2026-03-11), not the browser's.
    const user = userEvent.setup()
    const grant = makeGrant()
    const { sent } = mockApi([grant], (request) => created(request.body))
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: TODAY })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await screen.findByRole('status')
    expect(sentTasks(sent).map((task) => task.dueDate)).toEqual(['2026-03-11'])
  })

  it('given the API returns 400 keyed on dueDate, when submitting, then the message is shown on the due date field', async () => {
    // Arrange: the client-side check passes, but the API rejects the date (for example, Sydney midnight has passed).
    const user = userEvent.setup()
    const grant = makeGrant()
    mockApi([grant], () =>
      json(400, {
        code: 'validation',
        status: 400,
        title: 'One or more validation errors occurred.',
        errors: { dueDate: ['Due date cannot be in the past.'] },
      }),
    )
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-12' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await vi.waitFor(() =>
      expect(screen.getByLabelText('Due date')).toHaveAccessibleDescription('Due date cannot be in the past.'),
    )
  })

  it('given the API returns 409 BR-1, when submitting, then its title and detail are shown', async () => {
    // Arrange: the grant was closed by another officer after this screen loaded.
    const user = userEvent.setup()
    const grant = makeGrant()
    mockApi([grant], () =>
      json(409, {
        code: 'BR-1',
        status: 409,
        title: 'Grant is closed',
        detail: 'A task cannot be added to a closed grant.',
      }),
    )
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    expect(await screen.findByText('Grant is closed')).toBeInTheDocument()
    expect(screen.getByText('A task cannot be added to a closed grant.')).toBeInTheDocument()
  })

  it('given the API returns 409, when submitting, then the grant is reloaded', async () => {
    // Arrange: after the 409 the API's grant is closed, and the reload must show that.
    const user = userEvent.setup()
    const grant = makeGrant()
    const grants = [grant]
    const { sent } = mockApi(grants, () => {
      grants[0] = makeGrant({ status: 'Closed', closedAtUtc: '2026-03-10T23:00:00Z' })
      return json(409, { code: 'BR-1', status: 409, title: 'Grant is closed' })
    })
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await vi.waitFor(() => expect(screen.getByLabelText('Title')).toBeDisabled())
    expect(sent('GET', '/grants/grant-1')).toHaveLength(2)
  })

  it('given no response within 10 seconds, when submitting, then the banner is shown, the values are kept and the button is enabled again', async () => {
    // Arrange
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const grant = makeGrant()
    mockApi([grant], (request) => noResponse(request.signal))
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Act
    await act(() => vi.advanceTimersByTimeAsync(10_000))

    // Assert
    expect(screen.getByText(SERVER_PROBLEM_MESSAGE)).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Verify bank details')
    expect(screen.getByLabelText('Due date')).toHaveValue('2026-03-20')
    expect(screen.getByRole('button', { name: 'Add task' })).toBeEnabled()
  })

  it('given a timeout, when sending again, then the same task id is sent', async () => {
    // Arrange: the first request gets no response; the second one succeeds.
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const grant = makeGrant()
    let calls = 0
    const { sent } = mockApi([grant], (request) => (++calls === 1 ? noResponse(request.signal) : created(request.body)))
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })
    await user.click(screen.getByRole('button', { name: 'Add task' }))
    await act(() => vi.advanceTimersByTimeAsync(10_000))

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await screen.findByRole('status')
    const [first, second] = sentTasks(sent)
    expect(second.id).toBe(first.id)
  })

  it('given a successful create, when adding the next task, then a new task id is used', async () => {
    // Arrange
    const user = userEvent.setup()
    const grant = makeGrant()
    const { sent } = mockApi([grant], (request) => created(request.body))
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })
    await user.click(screen.getByRole('button', { name: 'Add task' }))
    await screen.findByText('Task "Verify bank details" added.')
    await fillForm(user, { title: 'Schedule site visit', dueDate: '2026-03-21' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await screen.findByText('Task "Schedule site visit" added.')
    const [first, second] = sentTasks(sent)
    expect(second.id).not.toBe(first.id)
  })

  it('given a successful create, when the response arrives, then the form clears and the grant reloads with the new task', async () => {
    // Arrange: the API's grant gains the task when it's created.
    const user = userEvent.setup()
    const grant = makeGrant()
    const grants = [grant]
    mockApi(grants, (request) => {
      const body = request.body as CreateTaskRequest
      const task = makeTask({ id: body.id, title: body.title, dueDate: body.dueDate, priority: body.priority })
      grants[0] = { ...grant, pendingTaskCount: 1, tasks: [task] }
      return json(201, task)
    })
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    expect(await screen.findByRole('row', { name: /Verify bank details/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('')
    expect(screen.getByLabelText('Due date')).toHaveValue('')
  })

  it('given the API returns 409 duplicate-task-id, when sending again, then a new task id is used and the values are kept', async () => {
    // Arrange: DESIGN.md §6.3. The first id belongs to a task that was already saved with other values.
    const user = userEvent.setup()
    const grant = makeGrant()
    let calls = 0
    const { sent } = mockApi([grant], (request) =>
      ++calls === 1
        ? json(409, { code: 'duplicate-task-id', status: 409, title: 'This task id is already used by a task with different details' })
        : created(request.body),
    )
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })
    await user.click(screen.getByRole('button', { name: 'Add task' }))
    await screen.findByText('This task id is already used by a task with different details')
    expect(screen.getByLabelText('Title')).toHaveValue('Verify bank details')

    // Act
    await user.click(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    await screen.findByRole('status')
    const [first, second] = sentTasks(sent)
    expect(second.id).not.toBe(first.id)
    expect(second.title).toBe(first.title)
  })

  it('given a request is waiting, when clicking Add twice, then one request is sent', async () => {
    // Arrange: the response doesn't arrive while the officer double-clicks.
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const grant = makeGrant()
    const { sent } = mockApi([grant], (request) => noResponse(request.signal))
    await renderScreen(grant)
    await fillForm(user, { title: 'Verify bank details', dueDate: '2026-03-20' })

    // Act
    await user.dblClick(screen.getByRole('button', { name: 'Add task' }))

    // Assert
    expect(sentTasks(sent)).toHaveLength(1)
  })
})
