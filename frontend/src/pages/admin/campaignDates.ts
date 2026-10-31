/**
 * Campaign dates are calendar days ("yyyy-MM-dd"), never moments: all arithmetic here is in UTC so a time zone
 * can never move a day.
 */

function toDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d))
}

function toIso(date: Date): string {
  return date.toISOString().slice(0, 10)
}

function addDays(iso: string, days: number): string {
  const date = toDate(iso)
  date.setUTCDate(date.getUTCDate() + days)
  return toIso(date)
}

function lastDayOfMonth(iso: string): string {
  const date = toDate(iso)
  return toIso(new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)))
}

/** "2026-09-30" → "30.09.2026" */
export function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}.${m}.${y}`
}

/** Inclusive length in days. */
export function dayCount(start: string, end: string): number {
  return Math.round((toDate(end).getTime() - toDate(start).getTime()) / 86_400_000) + 1
}

/**
 * The period right after a campaign. One that ends with its month is followed by the whole next month (a
 * campaign opened on 17 September and ending on the 30th is followed by 1–31 October); anything else keeps its
 * length and starts the day after.
 */
export function nextPeriod(start: string, end: string): { startDate: string; endDate: string } {
  const startDate = addDays(end, 1)
  return {
    startDate,
    endDate: end === lastDayOfMonth(end) ? lastDayOfMonth(startDate) : addDays(startDate, dayCount(start, end) - 1),
  }
}
