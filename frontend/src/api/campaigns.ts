import { isAxiosError } from 'axios'
import { api } from './client'
import type { CurrentCampaign } from '../types/campaign'

export async function getCurrentCampaign(): Promise<CurrentCampaign> {
  const { data } = await api.get<CurrentCampaign>('/api/campaigns/current')
  return data
}

/** Public-display categories for a failed campaign load. Never exposes raw errors. */
export type CampaignErrorKind = 'no-active' | 'multiple' | 'unavailable' | 'unexpected'

export function classifyCampaignError(err: unknown): CampaignErrorKind {
  if (!isAxiosError(err)) return 'unexpected'
  if (!err.response) return 'unavailable' // timeout, refused, DNS: nothing came back
  const code = (err.response.data as { code?: string } | undefined)?.code
  if (code === 'NO_ACTIVE_CAMPAIGN') return 'no-active'
  if (code === 'MULTIPLE_ACTIVE_CAMPAIGNS') return 'multiple'
  if (err.response.status === 502 || err.response.status === 503 || err.response.status === 504) return 'unavailable'
  return 'unexpected'
}
