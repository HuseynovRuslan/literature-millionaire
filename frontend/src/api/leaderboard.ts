import { api } from './client'
import type { Leaderboard } from '../types/leaderboard'

/** `limit`: only the first places (the result screen's preview); 'all': everyone who finished the campaign. */
export async function getLeaderboard(
  campaignId: number,
  limit: 5 | 'all',
  signal?: AbortSignal,
): Promise<Leaderboard> {
  const { data } = await api.get<Leaderboard>(`/api/campaigns/${campaignId}/leaderboard`, {
    params: limit === 'all' ? undefined : { limit },
    signal,
  })
  return data
}
