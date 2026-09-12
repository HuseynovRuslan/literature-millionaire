import { api } from './client'
import type { AnswerOption, AnswerResult, StartGameResponse } from '../types/game'

export async function startGame(): Promise<StartGameResponse> {
  const { data } = await api.post<StartGameResponse>('/api/game/start')
  return data
}

export async function submitAnswer(
  sessionId: string,
  questionId: number,
  selectedOption: AnswerOption,
): Promise<AnswerResult> {
  const { data } = await api.post<AnswerResult>(`/api/game/${sessionId}/answer`, {
    questionId,
    selectedOption,
  })
  return data
}

/** Reports that the displayed question's deadline passed without an answer. Idempotency is enforced server-side. */
export async function submitTimeout(sessionId: string, questionId: number): Promise<AnswerResult> {
  const { data } = await api.post<AnswerResult>(`/api/game/${sessionId}/timeout`, { questionId })
  return data
}
