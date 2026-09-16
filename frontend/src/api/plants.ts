import { api } from './client'
import type { PlantCredits } from '../types/plant'

/**
 * Photograph credits for the plant catalogue. Public, read-only: the licences the pictures are
 * published under require the author, source and licence to be visible to visitors.
 */
export async function getPlantCredits(signal?: AbortSignal): Promise<PlantCredits> {
  const { data } = await api.get<PlantCredits>('/api/plants/credits', { signal })
  return data
}
