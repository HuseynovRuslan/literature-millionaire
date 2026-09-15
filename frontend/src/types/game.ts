import type { Difficulty } from './question'

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
  /** ISO 8601 UTC. The backend judges lateness against this; the client only displays it. */
  questionExpiresAtUtc: string
  question: GameQuestion
  /** This start's attempt number and how many remain for the campaign. */
  attemptNumber: number
  remainingAttempts: number
}

/** Registration data sent with the start request. Never persisted in the browser. */
export interface StartGameInput {
  fullName: string
  phoneNumber: string
}

/** Final outcome, computed by the backend. rewardTitle is present only when passed. */
export interface QuizResult {
  correctAnswers: number
  totalQuestions: number
  passingScore: number
  passed: boolean
  rewardTitle: string | null
  /** Weighted score (Easy 1, Medium 2, Hard 3). Informational: passing is decided by correctAnswers. */
  pointsEarned: number
  maxPoints: number
}

/** Progression after an answer or timeout. Carries no correctness information until the final result. */
export interface AnswerResult {
  questionNumber: number
  timedOut: boolean
  isGameOver: boolean
  nextQuestionNumber: number | null
  nextQuestion: GameQuestion | null
  nextQuestionExpiresAtUtc: string | null
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
