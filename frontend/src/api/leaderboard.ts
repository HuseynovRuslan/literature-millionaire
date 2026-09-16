import { api } from './client'
import type { Leaderboard } from '../types/leaderboard'

export async function getLeaderboard(
  campaignId: number,
  limit: 5 | 10,
  signal?: AbortSignal,
): Promise<Leaderboard> {
  const { data } = await api.get<Leaderboard>(`/api/campaigns/${campaignId}/leaderboard`, {
    params: { limit },
    signal,
  })
  return data
}
