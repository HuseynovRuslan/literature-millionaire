import type { Difficulty } from './question'
import type { QuizModeRef } from './campaign'

export type AnswerOption = 'A' | 'B' | 'C' | 'D'

export const ANSWER_OPTIONS: AnswerOption[] = ['A', 'B', 'C', 'D']

/** Player-facing question: never carries the correct option or explanation. Options arrive pre-shuffled per session. */
export interface GameQuestion {
  id: number
  text: string
  optionA: string
  optionB: string
  optionC: string
  optionD: string
  difficulty: Difficulty
  category: string
  /** Optional illustration, rendered only through an <img>. */
  imageUrl?: string | null
  imageAltText?: string | null
}

export interface StartGameResponse {
  sessionId: string
  questionNumber: number
  totalQuestions: number
  passingScore: number
  secondsPerQuestion: number
  /**
   * ISO 8601. From the server this is its own UTC deadline; api/game.ts re-anchors it to THIS device's
   * clock on arrival, so by the time anything reads it, it is safe to compare with Date.now().
   */
  questionExpiresAtUtc: string
  /** The deadline as a duration from when the server wrote the response. Absent on an older server. */
  questionRemainingMs?: number
  question: GameQuestion
  /** This start's attempt number and how many remain for the campaign. */
  attemptNumber: number
  remainingAttempts: number
  /** Campaign actually played (the requested one, or the default when none was requested) and its quiz mode. */
  campaignId: number
  quizMode: QuizModeRef
}

/** Registration data sent with the start request. Never persisted in the browser. */
export interface StartGameInput {
  fullName: string
  phoneNumber: string
  /** Campaign selected on the category screen. Omitted only by legacy callers; the server owns every rule. */
  campaignId?: number
}

/**
 * One question replayed on the result screen.
 *
 * Reaches the client only inside {@link QuizResult}, which the backend sets when the last question
 * has closed. No in-game response carries it, so the correct answers stay sealed for the whole round.
 */
export interface QuizAnswerReview {
  /** 1-based position in the session. */
  questionNumber: number
  text: string
  imageUrl?: string | null
  imageAltText?: string | null
  /** Letters as the player saw them in this session (options are shuffled per session). */
  correctOption: AnswerOption
  correctAnswer: string
  /** null when the clock closed the question with nothing picked. */
  selectedOption: AnswerOption | null
  selectedAnswer: string | null
  isCorrect: boolean
  timedOut: boolean
  explanation?: string | null
  difficulty: Difficulty
  /** What a correct answer was worth. */
  points: number
}

/** Final outcome, computed by the backend. rewardTitle is present only when passed. */
export interface QuizResult {
  /** Campaign and rank are authoritative server values; the frontend never derives either one. */
  campaignId: number
  leaderboardPosition: number | null
  correctAnswers: number
  totalQuestions: number
  passingScore: number
  passed: boolean
  rewardTitle: string | null
  /** Weighted score (Easy 1, Medium 2, Hard 3). Informational: passing is decided by correctAnswers. */
  pointsEarned: number
  maxPoints: number
  /** Quiz mode the campaign belongs to; the category shown on the result screen comes from here. */
  quizMode: QuizModeRef
  /** Every question with its correct answer. Empty only for a result the server could not describe. */
  review: QuizAnswerReview[]
}

/** Progression after an answer or timeout. Carries no correctness information until the final result. */
export interface AnswerResult {
  questionNumber: number
  timedOut: boolean
  isGameOver: boolean
  nextQuestionNumber: number | null
  nextQuestion: GameQuestion | null
  nextQuestionExpiresAtUtc: string | null
  /** The next deadline as a duration from when the server wrote the response. Absent on an older server. */
  nextQuestionRemainingMs?: number | null
  result: QuizResult | null
}

export function optionText(q: GameQuestion, option: AnswerOption): string {
  switch (option) {
    case 'A': return q.optionA
    case 'B': return q.optionB
    case 'C': return q.optionC
    case 'D': return q.optionD
  }
}

/** 1000000 -> "1 000 000" (thin, locale-independent grouping). Used by the legacy PrizeLadder component. */
export function formatPoints(n: number): string {
  return n.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ')
}
