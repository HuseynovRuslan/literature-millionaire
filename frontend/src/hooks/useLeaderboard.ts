import { isAxiosError } from 'axios'
import { useCallback, useEffect, useState } from 'react'
import { getLeaderboard } from '../api/leaderboard'
import type { Leaderboard } from '../types/leaderboard'

export type LeaderboardLoad =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; data: Leaderboard }
  /** notFound: the API answered 404 (unknown campaign); retrying cannot help. */
  | { kind: 'error'; notFound: boolean }

/** Loads public leaderboard data without caching it in browser storage. */
export function useLeaderboard(campaignId: number | null, limit: 5 | 'all') {
  const [requestNumber, setRequestNumber] = useState(0)
  const requestKey = campaignId === null ? 'idle' : `${campaignId}:${limit}:${requestNumber}`
  const [settled, setSettled] = useState<{ requestKey: string; load: LeaderboardLoad }>({
    requestKey: 'idle',
    load: { kind: 'idle' },
  })

  useEffect(() => {
    if (campaignId === null) return

    const controller = new AbortController()
    getLeaderboard(campaignId, limit, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setSettled({ requestKey, load: { kind: 'ready', data } })
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return
        const notFound = isAxiosError(err) && err.response?.status === 404
        setSettled({ requestKey, load: { kind: 'error', notFound } })
      })

    return () => controller.abort()
  }, [campaignId, limit, requestKey])

  const retry = useCallback(() => setRequestNumber((current) => current + 1), [])
  const load: LeaderboardLoad = settled.requestKey === requestKey
    ? settled.load
    : campaignId === null
      ? { kind: 'idle' }
      : { kind: 'loading' }
  return { load, retry }
}
