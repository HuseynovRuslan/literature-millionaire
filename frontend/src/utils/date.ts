const MONTHS_AZ = ['yanvar', 'fevral', 'mart', 'aprel', 'may', 'iyun', 'iyul', 'avqust', 'sentyabr', 'oktyabr', 'noyabr', 'dekabr']

/** "2026-09-01".."2026-09-30" -> "1–30 sentyabr 2026"; across months -> "25 avqust – 5 oktyabr 2026". */
export function formatDateRange(startIso: string, endIso: string): string {
  const [sy, sm, sd] = startIso.split('-').map(Number)
  const [ey, em, ed] = endIso.split('-').map(Number)
  if (![sy, sm, sd, ey, em, ed].every(Number.isFinite)) return `${startIso} – ${endIso}`
  if (sy === ey && sm === em) return `${sd}–${ed} ${MONTHS_AZ[sm - 1]} ${sy}`
  if (sy === ey) return `${sd} ${MONTHS_AZ[sm - 1]} – ${ed} ${MONTHS_AZ[em - 1]} ${sy}`
  return `${sd} ${MONTHS_AZ[sm - 1]} ${sy} – ${ed} ${MONTHS_AZ[em - 1]} ${ey}`
}
