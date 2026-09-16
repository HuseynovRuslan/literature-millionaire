import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { isAxiosError } from 'axios'
import { startGame as apiStartGame, submitAnswer as apiSubmitAnswer, submitTimeout as apiSubmitTimeout } from '../api/game'
import type { QuizModeRef } from '../types/campaign'
import type { AnswerOption, AnswerResult, GameQuestion, QuizResult, StartGameInput } from '../types/game'
import { SessionInvalidError } from './errors'
import { clearActiveGame, loadActiveGame, saveActiveGame, type ActiveGameSnapshot } from './storage'

/**
 * Why an in-progress session became unusable. A stable code the UI can branch on, so the display
 * text (Azerbaijani, subject to wording changes) is never used to make a business decision.
 */
export type ExpiredReason = 'SESSION_NOT_FOUND' | 'UNEXPECTED_QUESTION' | 'GAME_OVER' | 'OTHER'

export interface GameState {
  status: 'idle' | 'starting' | 'playing' | 'finished' | 'expired'
  sessionId: string | null
  questionNumber: number
  totalQuestions: number
  passingScore: number
  secondsPerQuestion: number
  /** ISO UTC deadline of the visible question, set by the backend. */
  questionExpiresAtUtc: string | null
  question: GameQuestion | null
  /** Campaign and quiz mode of the in-progress session (set at start, kept through 'expired'). Null when idle. */
  campaignId: number | null
  quizMode: QuizModeRef | null
  /** Set when status is 'finished'. Authoritative: campaignId/quizMode/score here, not the fields above, decide the result screen. */
  result: QuizResult | null
  /** Set when status is 'expired': why the session can no longer be used. */
  expiredReason: ExpiredReason | null
  /** Human-readable detail for the expired screen, shown alongside (never instead of) the reason-driven copy. */
  expiredMessage: string | null
}

interface GameContextValue {
  state: GameState
  /** Starts a quiz for the given participant and campaign. Resolves true on success; on failure `error`/`errorCode` are set. */
  startGame: (input: StartGameInput) => Promise<boolean>
  submitAnswer: (option: AnswerOption) => Promise<AnswerResult>
  /** Reports the deadline of the visible question. Safe to retry with the same question. */
  submitTimeout: () => Promise<AnswerResult>
  advance: (result: AnswerResult) => void
  reset: () => void
  error: string | null
  /** Stable backend code of the last start failure (e.g. ATTEMPT_LIMIT_REACHED), or null. */
  errorCode: string | null
}

const initial: GameState = {
  status: 'idle',
  sessionId: null,
  questionNumber: 0,
  totalQuestions: 0,
  passingScore: 0,
  secondsPerQuestion: 30,
  questionExpiresAtUtc: null,
  question: null,
  campaignId: null,
  quizMode: null,
  result: null,
  expiredReason: null,
  expiredMessage: null,
}

function toSnapshot(s: GameState): ActiveGameSnapshot | null {
  if (s.status !== 'playing' || !s.sessionId || !s.question || !s.questionExpiresAtUtc || !s.campaignId || !s.quizMode) return null
  return {
    sessionId: s.sessionId,
    questionNumber: s.questionNumber,
    totalQuestions: s.totalQuestions,
    passingScore: s.passingScore,
    secondsPerQuestion: s.secondsPerQuestion,
    questionExpiresAtUtc: s.questionExpiresAtUtc,
    question: s.question,
    campaignId: s.campaignId,
    quizMode: s.quizMode,
  }
}

function fromSnapshot(snap: ActiveGameSnapshot): GameState {
  return { ...initial, ...snap, status: 'playing' }
}

/** Keeps sessionStorage in step with the state: playing -> saved, anything else -> cleared. */
function persist(s: GameState): GameState {
  const snap = toSnapshot(s)
  if (snap) saveActiveGame(snap)
  else clearActiveGame()
  return s
}

const GameContext = createContext<GameContextValue | null>(null)

export function GameProvider({ children }: { children: ReactNode }) {
  // Hydrate from sessionStorage so a refresh on /game shows the same unanswered question with its original deadline.
  const [state, setState] = useState<GameState>(() => {
    const snap = loadActiveGame()
    return snap ? fromSnapshot(snap) : initial
  })
  const [error, setError] = useState<string | null>(null)
  const [errorCode, setErrorCode] = useState<string | null>(null)
  const startingRef = useRef(false)

  const startGame = useCallback(async (input: StartGameInput) => {
    if (startingRef.current) return false // ignore repeated taps
    startingRef.current = true
    setError(null)
    setErrorCode(null)
    setState({ ...initial, status: 'starting' })
    try {
      const start = await apiStartGame(input)
      setState(persist({
        ...initial,
        status: 'playing',
        sessionId: start.sessionId,
        questionNumber: start.questionNumber,
        totalQuestions: start.totalQuestions,
        passingScore: start.passingScore,
        secondsPerQuestion: start.secondsPerQuestion,
        questionExpiresAtUtc: start.questionExpiresAtUtc,
        question: start.question,
        campaignId: start.campaignId,
        quizMode: start.quizMode,
      }))
      return true
    } catch (err) {
      setState(persist({ ...initial }))
      const status = isAxiosError(err) ? err.response?.status : undefined
      const data = isAxiosError(err) ? (err.response?.data as { code?: string; maxAttempts?: number; errors?: Record<string, string[]> } | undefined) : undefined
      const code = data?.code ?? (status === 400 ? 'VALIDATION' : null)
      setErrorCode(code)
      setError(
        status === 404 && code === 'NO_ACTIVE_CAMPAIGN'
          ? 'Bu kateqoriya artıq mövcud deyil.'
          : status === 404 && code === 'CAMPAIGN_NOT_FOUND'
            ? 'Bu kateqoriya artıq mövcud deyil.'
            : status === 409 && code === 'CAMPAIGN_NOT_ACTIVE'
              ? 'Bu kateqoriya hazırda aktiv deyil.'
              : code === 'ATTEMPT_LIMIT_REACHED'
                ? 'Bu kampaniyada artıq iştirak etmisiniz. Hər telefon nömrəsi ilə yalnız bir dəfə iştirak etmək mümkündür.'
                : code === 'ATTEMPT_CONFLICT'
                  ? 'Cəhd qeydə alına bilmədi. Zəhmət olmasa yenidən cəhd edin.'
                  : code === 'DATABASE_ERROR'
                    ? 'Müvəqqəti server xətası. Zəhmət olmasa yenidən cəhd edin.'
                    : status === 400
                      ? 'Ad, soyad və ya telefon nömrəsi düzgün deyil.'
                      : status === 409
                        ? 'Bu kampaniya üçün kifayət qədər sual yoxdur.'
                        : 'Oyunu başlatmaq mümkün olmadı. Server cavab vermir.',
      )
      return false
    } finally {
      startingRef.current = false
    }
  }, [])

  /** Shared post-processing for answer and timeout responses: persist the next position, map dead-session errors. */
  const settle = useCallback(
    async (request: () => Promise<AnswerResult>) => {
      if (!state.sessionId || !state.question) throw new Error('Aktiv oyun yoxdur.')
      try {
        const result = await request()
        // Persist the post-answer position right away, so a refresh during the
        // transition lands on the next question (with its real deadline) or a clean slate.
        if (result.isGameOver || !result.nextQuestion || !result.nextQuestionExpiresAtUtc) {
          clearActiveGame()
        } else if (state.campaignId && state.quizMode) {
          saveActiveGame({
            sessionId: state.sessionId,
            questionNumber: result.nextQuestionNumber ?? state.questionNumber + 1,
            totalQuestions: state.totalQuestions,
            passingScore: state.passingScore,
            secondsPerQuestion: state.secondsPerQuestion,
            questionExpiresAtUtc: result.nextQuestionExpiresAtUtc,
            question: result.nextQuestion,
            campaignId: state.campaignId,
            quizMode: state.quizMode,
          })
        }
        return result
      } catch (err) {
        const status = isAxiosError(err) ? err.response?.status : undefined
        const code = isAxiosError(err) ? (err.response?.data as { code?: string } | undefined)?.code : undefined
        // QUESTION_TIME_REMAINING is a timing disagreement of a few ms, not a dead session: let the caller retry.
        if (status === 404 || (status === 409 && code !== 'QUESTION_TIME_REMAINING')) {
          clearActiveGame()
          const reason: ExpiredReason =
            status === 404 ? 'SESSION_NOT_FOUND' : code === 'UNEXPECTED_QUESTION' ? 'UNEXPECTED_QUESTION' : code === 'GAME_OVER' ? 'GAME_OVER' : 'OTHER'
          setState((s) => ({
            ...initial,
            status: 'expired',
            // The category the expired session belonged to is kept (not personal data) so the
            // "next participant" action on the expired screen can return to the same category.
            campaignId: s.campaignId,
            quizMode: s.quizMode,
            expiredReason: reason,
            expiredMessage:
              reason === 'SESSION_NOT_FOUND'
                ? 'Oyun sessiyasının vaxtı bitib.'
                : reason === 'UNEXPECTED_QUESTION'
                  ? 'Bu sual artıq bağlanıb və oyun davam etdirilə bilmir.'
                  : reason === 'GAME_OVER'
                    ? 'Bu oyun artıq bitib.'
                    : 'Oyun sessiyası artıq etibarlı deyil.',
          }))
          throw new SessionInvalidError()
        }
        throw err
      }
    },
    [state.sessionId, state.question, state.questionNumber, state.totalQuestions, state.passingScore, state.secondsPerQuestion, state.campaignId, state.quizMode],
  )

  const submitAnswer = useCallback(
    (option: AnswerOption) => settle(() => apiSubmitAnswer(state.sessionId!, state.question!.id, option)),
    [settle, state.sessionId, state.question],
  )

  const submitTimeout = useCallback(
    () => settle(() => apiSubmitTimeout(state.sessionId!, state.question!.id)),
    [settle, state.sessionId, state.question],
  )

  const advance = useCallback((r: AnswerResult) => {
    setState((s) => {
      if (r.isGameOver || !r.nextQuestion || !r.nextQuestionExpiresAtUtc) {
        return persist({
          ...s,
          status: 'finished',
          question: null,
          questionExpiresAtUtc: null,
          result: r.result ?? {
            campaignId: s.campaignId ?? 0,
            leaderboardPosition: null,
            correctAnswers: 0,
            totalQuestions: s.totalQuestions,
            passingScore: s.passingScore,
            passed: false,
            rewardTitle: null,
            pointsEarned: 0,
            maxPoints: 0,
            quizMode: s.quizMode ?? { id: 0, slug: '', title: '' },
          },
        })
      }
      return persist({
        ...s,
        questionNumber: r.nextQuestionNumber ?? s.questionNumber + 1,
        questionExpiresAtUtc: r.nextQuestionExpiresAtUtc,
        question: r.nextQuestion,
      })
    })
  }, [])

  const reset = useCallback(() => {
    setError(null)
    setErrorCode(null)
    setState(persist({ ...initial }))
  }, [])

  const value = useMemo(
    () => ({ state, startGame, submitAnswer, submitTimeout, advance, reset, error, errorCode }),
    [state, startGame, submitAnswer, submitTimeout, advance, reset, error, errorCode],
  )

  return <GameContext.Provider value={value}>{children}</GameContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useGame(): GameContextValue {
  const ctx = useContext(GameContext)
  if (!ctx) throw new Error('useGame must be used inside <GameProvider>')
  return ctx
}
