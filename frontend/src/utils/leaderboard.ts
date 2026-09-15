const UNIT_SUFFIX: Record<number, string> = {
  1: 'ci',
  2: 'ci',
  3: 'cü',
  4: 'cü',
  5: 'ci',
  6: 'cı',
  7: 'ci',
  8: 'ci',
  9: 'cu',
}

const TENS_SUFFIX: Record<number, string> = {
  1: 'cu',
  2: 'ci',
  3: 'cu',
  4: 'cı',
  5: 'ci',
  6: 'cı',
  7: 'ci',
  8: 'ci',
  9: 'cı',
}

export function formatRank(rank: number): string {
  const lastDigit = rank % 10
  if (lastDigit !== 0) return `${rank}-${UNIT_SUFFIX[lastDigit]}`

  const tensDigit = Math.floor(rank / 10) % 10
  if (tensDigit !== 0) return `${rank}-${TENS_SUFFIX[tensDigit]}`
  if (rank % 1000 !== 0) return `${rank}-cü`
  if (rank % 1_000_000 !== 0) return `${rank}-ci`
  return `${rank}-cu`
}
