import { api } from './client'
import type { AnswerOption, AnswerResult, StartGameInput, StartGameResponse } from '../types/game'

/**
 * Turns the server's deadline into one this device can count down to.
 *
 * The server sends the deadline twice: as an absolute UTC time and as a duration from the moment it
 * wrote the response. The countdown used to subtract this device's clock from the absolute time - two
 * different clocks in one subtraction. A phone running two seconds slow showed two seconds the server
 * had already spent, and a player who answered with time on the screen was told their time had run out.
 *
 * A duration survives the trip; an absolute time does not. So the duration is anchored to this device's
 * clock here, at the moment the response arrives - not later, when the question is actually put on
 * screen, or the wait in between would be handed to the player as extra time. Everything downstream
 * (the countdown, the refresh snapshot) then compares this clock with itself and needs no change.
 *
 * Falls back to the absolute time when the duration is missing, which is what an older server sends.
 */
function localDeadline(remainingMs: number | null | undefined, absoluteUtc: string): string
function localDeadline(remainingMs: number | null | undefined, absoluteUtc: string | null): string | null
function localDeadline(remainingMs: number | null | undefined, absoluteUtc: string | null): string | null {
  if (absoluteUtc === null) return null
  if (typeof remainingMs !== 'number' || !Number.isFinite(remainingMs)) return absoluteUtc
  return new Date(Date.now() + Math.max(0, remainingMs)).toISOString()
}

function withLocalNextDeadline(result: AnswerResult): AnswerResult {
  return { ...result, nextQuestionExpiresAtUtc: localDeadline(result.nextQuestionRemainingMs, result.nextQuestionExpiresAtUtc) }
}

export async function startGame(input: StartGameInput): Promise<StartGameResponse> {
  const { data } = await api.post<StartGameResponse>('/api/game/start', input)
  return { ...data, questionExpiresAtUtc: localDeadline(data.questionRemainingMs, data.questionExpiresAtUtc) }
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
  return withLocalNextDeadline(data)
}

/** Reports that the displayed question's deadline passed without an answer. Idempotency is enforced server-side. */
export async function submitTimeout(sessionId: string, questionId: number): Promise<AnswerResult> {
  const { data } = await api.post<AnswerResult>(`/api/game/${sessionId}/timeout`, { questionId })
  return withLocalNextDeadline(data)
}
