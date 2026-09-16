import { useEffect, useRef, useState } from 'react'
import { Navigate } from 'react-router-dom'
import AnswerButton, { type AnswerVisual } from '../components/AnswerButton'
import GameStageHeader from '../components/game/GameStageHeader'
import { FlagStripe, Octagram } from '../components/arena/NationalMotifs'
import GameResult from './GameResult'
import SessionExpired from './SessionExpired'
import { useGame } from '../game/GameContext'
import * as sound from '../game/sound'
import { SessionInvalidError } from '../game/errors'
import { ANSWER_OPTIONS, optionText, type AnswerOption, type AnswerResult } from '../types/game'

// The dramatic pause after an answer is locked in. The question is already closed and the clock has
// stopped; this is the beat of silence before the next question, with the heartbeat still running.
// Nothing about correctness is shown here - the round stays sealed until the result screen.
const TRANSITION_MS = 2600
const TIMEOUT_RETRY_MS = 2000
// Answer taps are ignored this long after a question appears, so a stray second tap from the
// previous screen (e.g. a double tap on "NÖVBƏTİ İŞTİRAKÇI") cannot answer the first question.
// Frontend-only: the server deadline and the countdown are unaffected.
const QUESTION_INPUT_GUARD_MS = 400
// Presentation only: the closed question starts its exit animation this long before the next one arrives.
const LEAVE_ANIMATION_MS = 520

type Phase =
  | { kind: 'open' }
  | { kind: 'sending'; selected: AnswerOption | null } // answer or timeout in flight, buttons locked
  | { kind: 'retrying'; attempt: number } // timeout call failed on the network; retrying the same question
  | { kind: 'closed'; selected: AnswerOption | null; timedOut: boolean }

export default function GamePage() {
  const { state, submitAnswer, submitTimeout, advance } = useGame()
  const [phase, setPhase] = useState<Phase>({ kind: 'open' })
  const [now, setNow] = useState(() => Date.now())
  const [sendError, setSendError] = useState<string | null>(null)
  const timer = useRef<number | null>(null)
  const leaveTimer = useRef<number | null>(null)
  const [leaving, setLeaving] = useState(false)
  const inFlight = useRef(false)
  const timeoutSentFor = useRef<number | null>(null)
  const questionShownAt = useRef(Date.now())

  const q = state.question
  const deadline = state.questionExpiresAtUtc ? Date.parse(state.questionExpiresAtUtc) : null
  const remainingMs = deadline === null ? Infinity : Math.max(0, deadline - now)
  const total = state.secondsPerQuestion
  // Cap at the API value: network latency can put the deadline a few ms beyond N seconds, which would round up to N+1.
  const remainingSec = Math.min(total, Math.ceil(remainingMs / 1000))
  // Warning window scales with the question time: 5 s of 10 or 15, 10 s of 30.
  const urgentSeconds = Math.max(5, Math.round(total / 3))
  const [imageFailed, setImageFailed] = useState(false)
  const [soundOn, setSoundOn] = useState(() => sound.isEnabled())

  // Visual countdown only: it mirrors the backend deadline; the backend decides lateness.
  useEffect(() => {
    if (state.status !== 'playing') return
    const id = window.setInterval(() => setNow(Date.now()), 200)
    return () => window.clearInterval(id)
  }, [state.status])

  // Atmosphere: the heartbeat quickens with the step of the climb and with the clock. It knows nothing
  // about answers, and it stops as soon as the screen is left.
  useEffect(() => {
    if (state.status !== 'playing' || !soundOn) return
    sound.updateRound(state.questionNumber, state.totalQuestions, remainingSec, total)
  }, [state.status, state.questionNumber, state.totalQuestions, remainingSec, total, soundOn])

  useEffect(() => () => sound.stopAll(), [])

  // The quiz is over: one cue, then quiet.
  useEffect(() => {
    if (state.status === 'finished') sound.finish(state.result?.passed ?? false)
    if (state.status === 'expired') sound.stopAll()
  }, [state.status, state.result?.passed])

  // Clear any pending transition or retry when the page unmounts.
  useEffect(() => () => {
    if (timer.current) window.clearTimeout(timer.current)
    if (leaveTimer.current) window.clearTimeout(leaveTimer.current)
  }, [])

  // New question: reset local phase and the once-per-question timeout guard.
  useEffect(() => {
    setPhase({ kind: 'open' })
    setLeaving(false)
    setSendError(null)
    setImageFailed(false)
    timeoutSentFor.current = null
    questionShownAt.current = Date.now()
  }, [q?.id])

  function scheduleAdvance(result: AnswerResult) {
    timer.current = window.setTimeout(() => advance(result), TRANSITION_MS)
    // The last question hands over to the result screen, which has its own entrance; no exit needed there.
    if (!result.isGameOver) leaveTimer.current = window.setTimeout(() => setLeaving(true), TRANSITION_MS - LEAVE_ANIMATION_MS)
  }

  async function choose(option: AnswerOption) {
    if (phase.kind !== 'open' || inFlight.current || remainingMs <= 0) return
    if (Date.now() - questionShownAt.current < QUESTION_INPUT_GUARD_MS) return // input guard, see constant above
    inFlight.current = true
    // A tap is the gesture browsers require before audio may start.
    sound.resume()
    sound.lockIn()
    setPhase({ kind: 'sending', selected: option })
    setSendError(null)
    try {
      const result = await submitAnswer(option)
      setPhase({ kind: 'closed', selected: option, timedOut: result.timedOut })
      scheduleAdvance(result)
    } catch (err) {
      // Session errors already switched the screen; anything else: let the player tap again while time remains.
      if (!(err instanceof SessionInvalidError)) {
        setPhase({ kind: 'open' })
        setSendError('Cavab göndərilmədi. Yenidən toxunun.')
      }
    } finally {
      inFlight.current = false
    }
  }

  async function reportTimeout(attempt: number) {
    if (!q || inFlight.current) return
    inFlight.current = true
    setPhase(attempt === 0 ? { kind: 'sending', selected: null } : { kind: 'retrying', attempt })
    try {
      const result = await submitTimeout()
      inFlight.current = false
      setPhase({ kind: 'closed', selected: null, timedOut: true })
      scheduleAdvance(result)
    } catch (err) {
      inFlight.current = false
      if (err instanceof SessionInvalidError) return
      // Network failure (or a QUESTION_TIME_REMAINING disagreement of a few ms): retry the same
      // question after a pause. The buttons stay locked; no extra answering time is granted.
      setSendError('Şəbəkə xətası. Yenidən cəhd edilir…')
      setPhase({ kind: 'retrying', attempt: attempt + 1 })
      timer.current = window.setTimeout(() => void reportTimeout(attempt + 1), TIMEOUT_RETRY_MS)
    }
  }

  // Deadline reached (also right after a refresh with an already-expired deadline): lock and report exactly once.
  useEffect(() => {
    if (state.status !== 'playing' || !q || phase.kind !== 'open') return
    if (remainingMs > 0 || timeoutSentFor.current === q.id) return
    timeoutSentFor.current = q.id
    void reportTimeout(0)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remainingMs, state.status, q?.id, phase.kind])

  if (state.status === 'idle') return <Navigate to="/" replace />
  if (state.status === 'expired') return <SessionExpired />
  if (state.status === 'finished' || (state.status === 'starting' && !q)) return <GameResult />
  if (!q) return <GameResult />

  const locked = phase.kind !== 'open' || remainingMs <= 0
  const urgent = remainingSec <= urgentSeconds
  const hasImage = typeof q.imageUrl === 'string' && q.imageUrl.length > 0
  const selected = phase.kind === 'sending' || phase.kind === 'closed' ? phase.selected : null
  // Presentation only: long question texts use a smaller type so they fit without truncation.
  const longText = q.text.length > 90

  function visualFor(option: AnswerOption): AnswerVisual {
    if (option === selected) return 'selected'
    return locked ? 'dimmed' : 'idle'
  }


  const alertClass =
    'inline-flex items-center gap-3 rounded-2xl bg-bad/15 px-5 py-2.5 text-left font-semibold text-fg ring-1 ring-bad/60 max-sm:gap-2 max-sm:rounded-xl max-sm:px-3 max-sm:py-1.5'
  const alertIcon = (
    <span aria-hidden className="grid size-[1.5em] shrink-0 place-items-center rounded-full bg-bad font-extrabold text-ink-950">!</span>
  )

  return (
    <main className="kiosk arena flex flex-col" data-testid="game-page">
      <FlagStripe className="relative z-20 shrink-0" />
      <section
        key={q.id}
        className="q-stage relative z-10 mx-auto flex min-h-0 w-full max-w-[120rem] flex-1 flex-col px-[clamp(1.2rem,2.6vw,3.4rem)] pb-[clamp(0.8rem,1.8vh,1.6rem)] pt-[clamp(0.8rem,1.8vh,1.6rem)] max-sm:px-3 max-sm:pb-[calc(0.6rem_+_var(--safe-bottom))] max-sm:pt-[calc(0.6rem_+_var(--safe-top))]"
        data-leaving={leaving}
      >
        <GameStageHeader
          questionNumber={state.questionNumber}
          totalQuestions={state.totalQuestions}
          remainingSec={remainingSec}
          fraction={Math.min(1, remainingMs / (total * 1000))}
          urgent={urgent}
          expired={remainingMs <= 0}
          quizModeTitle={state.quizMode?.title ?? ''}
          soundOn={soundOn}
          onToggleSound={() => {
            const next = !soundOn
            sound.setEnabled(next)
            setSoundOn(next)
            if (next) sound.resume()
          }}
        />

        {/* Decorative "Sual N" burst on every new question; the real counter is in the header. */}
        <div className="q-splash" aria-hidden="true">
          <div className="q-splash-inner relative overflow-hidden">
            <Octagram className="spin-slow pointer-events-none absolute left-1/2 top-1/2 size-[140%] -translate-x-1/2 -translate-y-1/2 text-white/10" />
            <span className="font-sans text-[clamp(0.8rem,1.1vw,1.1rem)] font-extrabold uppercase tracking-[0.3em] text-white/80 max-sm:text-[0.7rem]">Sual</span>
            <span className="font-display text-[clamp(4rem,9vw,8rem)] font-extrabold leading-none tabular-nums max-sm:text-[3.5rem]">
              {state.questionNumber}<span className="text-[0.4em] text-white/60"> / {state.totalQuestions}</span>
            </span>
          </div>
        </div>

        <div
          data-testid="question-card"
          className={`q-card q-card-enter relative mb-[clamp(1.1rem,2.4vh,2rem)] mt-[clamp(0.8rem,2vh,1.6rem)] min-h-0 flex-1 rounded-[clamp(1.4rem,2vw,2.2rem)] px-[clamp(1.4rem,3vw,4rem)] py-[clamp(1rem,2.4vh,2.2rem)] max-sm:mb-3 max-sm:mt-2.5 max-sm:rounded-3xl max-sm:px-3.5 max-sm:pb-3 max-sm:pt-6 ${
            hasImage
              ? 'grid grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] items-center gap-[clamp(1.4rem,3vw,4rem)] max-lg:flex max-lg:flex-col max-lg:justify-center max-lg:gap-3'
              : 'flex flex-col justify-center'
          }`}
        >
          <span className="absolute left-1/2 top-0 -translate-x-1/2 -translate-y-1/2 rounded-full bg-brand px-4 py-1.5 font-display text-[clamp(0.72rem,0.85vw,0.9rem)] font-bold uppercase tracking-[0.12em] text-white shadow-[0_4px_0_#4a33c9] max-sm:px-3 max-sm:py-1 max-sm:text-[0.62rem]" aria-hidden="true">
            Sual {state.questionNumber}
          </span>
          <h1
            id="question-text"
            lang="az"
            className={`q-content-enter font-display font-bold leading-[1.2] text-ink-900 [overflow-wrap:anywhere] [text-wrap:balance] ${
              hasImage
                ? 'text-[clamp(1.5rem,min(2.4vw,4.6vh),3rem)] max-lg:text-center max-sm:text-[1.1rem]'
                : `mx-auto max-w-[32ch] text-center ${longText ? 'text-[clamp(1.6rem,min(2.6vw,5vh),3.2rem)] max-sm:text-[1.1rem]' : 'text-[clamp(1.9rem,min(3.2vw,6vh),4rem)] max-sm:text-[1.3rem]'}`
            }`}
          >
            {q.text}
          </h1>
          {hasImage && (
            // The picture takes the space the card has (object-contain, never cropped or stretched); on the kiosk it sits left.
            <figure className="q-content-enter flex h-full min-h-0 w-full items-center justify-center lg:order-first max-lg:flex-1" data-testid="question-image">
              {imageFailed ? (
                <p role="img" aria-label={q.imageAltText ?? 'Təsvir'} className="rounded-2xl bg-ink-900/5 px-8 py-6 text-[clamp(1rem,1.3vw,1.3rem)] font-semibold text-ink-600 ring-1 ring-ink-900/10">
                  Təsvir yüklənmədi
                </p>
              ) : (
                <img
                  src={q.imageUrl ?? undefined}
                  alt={q.imageAltText ?? ''}
                  onError={() => setImageFailed(true)}
                  className="max-h-full max-w-full rounded-2xl bg-white object-contain shadow-[0_0_0_4px_#ffffff,0_0_0_6px_rgba(123,97,255,0.35),0_1rem_2rem_-0.8rem_rgba(27,20,64,0.45)] max-sm:rounded-xl"
                />
              )}
            </figure>
          )}
        </div>

        <div data-testid="answers" className="grid grid-cols-2 gap-[clamp(0.8rem,1.4vw,1.4rem)] max-sm:gap-2.5">
          {ANSWER_OPTIONS.map((o, i) => (
            <AnswerButton key={o} index={i} option={o} text={optionText(q, o)} visual={visualFor(o)} disabled={locked} onSelect={choose} />
          ))}
        </div>

        <footer
          data-testid="game-status"
          aria-live="polite"
          className="mt-[clamp(0.7rem,1.6vh,1.2rem)] flex min-h-[clamp(2.8rem,5vh,3.8rem)] items-center justify-center text-center text-[clamp(0.95rem,1.15vw,1.2rem)] max-sm:mt-2 max-sm:min-h-[2.6rem] max-sm:text-[0.78rem] max-sm:leading-snug"
        >
          {phase.kind === 'closed' && (
            <p className="pop inline-flex items-center gap-3 rounded-full bg-white px-6 py-2.5 font-display text-[1.1em] font-bold text-ink-900 shadow-[0_4px_0_rgba(123,97,255,0.5)] max-sm:gap-2 max-sm:px-4 max-sm:py-1.5 max-sm:text-[1.15em]">
              <span aria-hidden className="size-2.5 shrink-0 animate-pulse rounded-full bg-brand motion-reduce:animate-none" />
              {phase.timedOut ? 'Vaxt bitdi. Növbəti sual…' : 'Cavab qeydə alındı. Növbəti sual…'}
            </p>
          )}
          {phase.kind === 'sending' && <p className="font-semibold text-fg-2">Göndərilir…</p>}
          {phase.kind === 'retrying' && (
            <p role="alert" className={alertClass}>{alertIcon}{sendError}</p>
          )}
          {phase.kind === 'open' && sendError && (
            <p role="alert" className={alertClass}>{alertIcon}{sendError}</p>
          )}
          {phase.kind === 'open' && !sendError && (
            <p className="font-medium text-fg-3">
              Hər sual üçün {total} saniyə. Keçid üçün ən azı{' '}
              <span className="font-extrabold tabular-nums text-sun">{state.passingScore}</span> / {state.totalQuestions} düzgün cavab
              lazımdır. Nəticə sonda açıqlanır.
            </p>
          )}
        </footer>
      </section>
    </main>
  )
}
