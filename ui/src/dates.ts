import type { CalendarDate } from './api/types'

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

/**
 * "2026-03-11" → "11 March 2026". Built from the text itself, never through a JavaScript Date, which would read the
 * value as midnight UTC and show the previous day west of UTC (DESIGN.md §5). Anything that isn't "YYYY-MM-DD" is
 * shown as it is.
 */
export function formatCalendarDate(date: CalendarDate): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date)
  if (match === null) {
    return date
  }

  const [, year, month, day] = match
  const monthName = MONTHS[Number(month) - 1]
  return monthName === undefined ? date : `${Number(day)} ${monthName} ${year}`
}
