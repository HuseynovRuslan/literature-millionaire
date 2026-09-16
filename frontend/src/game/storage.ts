import type { GameQuestion } from '../types/game'
import type { QuizModeRef } from '../types/campaign'

/** Snapshot of an active, unanswered question. Lives in sessionStorage (per tab). */
export interface ActiveGameSnapshot {
  sessionId: string
  questionNumber: number
  totalQuestions: number
  passingScore: number
  secondsPerQuestion: number
  /** ISO 8601 UTC deadline of the visible question. Restored as-is: a refresh never grants more time. */
  questionExpiresAtUtc: string
  question: GameQuestion
  /** Campaign being played and its quiz mode. Restored so a refreshed/resumed session still knows its category. */
  campaignId: number
  quizMode: QuizModeRef
}

// v3 adds campaignId/quizMode (category selection, Task 15B). A v2 snapshot has neither field and is
// deliberately treated as unreadable rather than partially restored: resuming into a game screen that
// cannot even name its own category is worse than asking the participant to start again.
const KEY = 'lm.activeGame.v3'
const LEGACY_KEYS = ['lm.activeGame.v1', 'lm.activeGame.v2']
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const OPTION_KEYS = ['optionA', 'optionB', 'optionC', 'optionD'] as const

function isInt(v: unknown, min: number, max: number): v is number {
  return typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max
}

function isNonEmptyString(v: unknown): v is string {
  return typeof v === 'string' && v.length > 0
}

function isQuestion(v: unknown): v is GameQuestion {
  if (!v || typeof v !== 'object') return false
  const q = v as Record<string, unknown>
  return (
    isInt(q.id, 1, Number.MAX_SAFE_INTEGER) &&
    typeof q.text === 'string' && q.text.length > 0 &&
    OPTION_KEYS.every((k) => typeof q[k] === 'string') &&
    typeof q.difficulty === 'string' &&
    typeof q.category === 'string' &&
    (q.imageUrl === undefined || q.imageUrl === null || typeof q.imageUrl === 'string') &&
    (q.imageAltText === undefined || q.imageAltText === null || typeof q.imageAltText === 'string')
  )
}

function isQuizModeRef(v: unknown): v is QuizModeRef {
  if (!v || typeof v !== 'object') return false
  const m = v as Record<string, unknown>
  return isInt(m.id, 1, Number.MAX_SAFE_INTEGER) && isNonEmptyString(m.slug) && isNonEmptyString(m.title)
}

function isSnapshot(v: unknown): v is ActiveGameSnapshot {
  if (!v || typeof v !== 'object') return false
  const s = v as Record<string, unknown>
  return (
    typeof s.sessionId === 'string' && GUID.test(s.sessionId) &&
    isInt(s.totalQuestions, 1, 100) &&
    isInt(s.questionNumber, 1, s.totalQuestions) &&
    isInt(s.passingScore, 1, s.totalQuestions) &&
    isInt(s.secondsPerQuestion, 1, 3600) &&
    typeof s.questionExpiresAtUtc === 'string' && Number.isFinite(Date.parse(s.questionExpiresAtUtc)) &&
    isQuestion(s.question) &&
    isInt(s.campaignId, 1, Number.MAX_SAFE_INTEGER) &&
    isQuizModeRef(s.quizMode)
  )
}

/** Returns the stored snapshot, or null. Anything unreadable, partial or malformed (including any pre-v3 shape) is removed. */
export function loadActiveGame(): ActiveGameSnapshot | null {
  try {
    for (const k of LEGACY_KEYS) sessionStorage.removeItem(k)
    const raw = sessionStorage.getItem(KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (isSnapshot(parsed)) return parsed
    sessionStorage.removeItem(KEY)
    return null
  } catch {
    try { sessionStorage.removeItem(KEY) } catch { /* storage unavailable */ }
    return null
  }
}

export function saveActiveGame(snapshot: ActiveGameSnapshot): void {
  try { sessionStorage.setItem(KEY, JSON.stringify(snapshot)) } catch { /* storage unavailable: play without persistence */ }
}

export function clearActiveGame(): void {
  try { sessionStorage.removeItem(KEY) } catch { /* ignore */ }
}
