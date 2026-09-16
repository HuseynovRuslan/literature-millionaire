import { useEffect, useRef, useState } from 'react'
import { Navigate } from 'react-router-dom'
import AnswerButton, { type AnswerVisual } from '../components/AnswerButton'
import GameStageHeader from '../components/game/GameStageHeader'
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
  useEffect(() => () => { if (timer.current) window.clearTimeout(timer.current) }, [])

  // New question: reset local phase and the once-per-question timeout guard.
  useEffect(() => {
    setPhase({ kind: 'open' })
    setSendError(null)
    setImageFailed(false)
    timeoutSentFor.current = null
    questionShownAt.current = Date.now()
  }, [q?.id])

  function scheduleAdvance(result: AnswerResult) {
    timer.current = window.setTimeout(() => advance(result), TRANSITION_MS)
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
    'inline-flex items-center gap-3 rounded-2xl bg-[rgba(107,34,48,0.7)] px-5 py-2.5 text-left font-medium text-[#fbf6ec] ring-1 ring-[#e8959c] max-sm:gap-2 max-sm:rounded-xl max-sm:px-3 max-sm:py-1.5'
  const alertIcon = (
    <span aria-hidden className="grid size-[1.5em] shrink-0 place-items-center rounded-full bg-[#fbf6ec] font-bold text-[#6b2230]">!</span>
  )

  return (
    <main className="kiosk game-stage flex flex-col" data-testid="game-page">
      <section
        key={q.id}
        className="rise relative z-10 mx-auto flex min-h-0 w-full max-w-[120rem] flex-1 flex-col px-[clamp(1.2rem,2.6vw,3.4rem)] pb-[clamp(0.8rem,1.8vh,1.6rem)] pt-[clamp(0.8rem,1.8vh,1.6rem)] max-sm:px-3 max-sm:pb-[calc(0.6rem_+_var(--safe-bottom))] max-sm:pt-[calc(0.6rem_+_var(--safe-top))]"
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

        <div
          data-testid="question-card"
          className={`gs-question-card relative my-[clamp(0.6rem,1.6vh,1.4rem)] min-h-0 flex-1 px-[clamp(1.4rem,3vw,4rem)] py-[clamp(1rem,2.4vh,2.2rem)] max-sm:my-2 max-sm:px-3.5 max-sm:py-3 ${
            hasImage
              ? 'grid grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)] items-center gap-[clamp(1.4rem,3vw,4rem)] max-lg:flex max-lg:flex-col max-lg:justify-center max-lg:gap-3'
              : 'flex flex-col justify-center'
          }`}
        >
          <h1
            id="question-text"
            lang="az"
            className={`font-display font-semibold leading-[1.15] text-[#f5f0e6] [overflow-wrap:anywhere] [text-wrap:balance] ${
              hasImage
                ? 'text-[clamp(1.9rem,min(2.9vw,5.4vh),3.6rem)] max-lg:text-center max-sm:text-[1.3rem]'
                : `mx-auto max-w-[34ch] text-center ${longText ? 'text-[clamp(2rem,min(3.1vw,5.8vh),3.9rem)] max-sm:text-[1.3rem]' : 'text-[clamp(2.3rem,min(3.8vw,7vh),4.8rem)] max-sm:text-[1.5rem]'}`
            }`}
          >
            {q.text}
          </h1>
          {hasImage && (
            // The picture takes the space the card has (object-contain, never cropped or stretched); on the kiosk it sits left.
            <figure className="flex h-full min-h-0 w-full items-center justify-center lg:order-first max-lg:flex-1" data-testid="question-image">
              {imageFailed ? (
                <p role="img" aria-label={q.imageAltText ?? 'Təsvir'} className="rounded-xl border border-[rgba(227,201,143,0.3)] bg-white/5 px-8 py-6 text-[clamp(1rem,1.3vw,1.3rem)] text-[#a7aec0]">
                  Təsvir yüklənmədi
                </p>
              ) : (
                <img
                  src={q.imageUrl ?? undefined}
                  alt={q.imageAltText ?? ''}
                  onError={() => setImageFailed(true)}
                  className="max-h-full max-w-full rounded-xl border border-[rgba(227,201,143,0.55)] bg-white object-contain shadow-[0_0_0_6px_rgba(12,18,36,0.9),0_0_0_7px_rgba(227,201,143,0.18),0_1.2rem_2.6rem_-1rem_rgba(0,0,0,0.8)] max-sm:rounded-lg"
                />
              )}
            </figure>
          )}
        </div>

        <div data-testid="answers" className="grid grid-cols-2 gap-[clamp(0.6rem,1.2vw,1.3rem)] max-sm:gap-2">
          {ANSWER_OPTIONS.map((o) => (
            <AnswerButton key={o} option={o} text={optionText(q, o)} visual={visualFor(o)} disabled={locked} onSelect={choose} />
          ))}
        </div>

        <footer
          data-testid="game-status"
          aria-live="polite"
          className="mt-[clamp(0.6rem,1.4vh,1.1rem)] flex min-h-[clamp(2.8rem,5vh,3.8rem)] items-center justify-center text-center text-[clamp(1rem,1.3vw,1.35rem)] max-sm:mt-2 max-sm:min-h-[2.6rem] max-sm:text-[0.78rem] max-sm:leading-snug"
        >
          {phase.kind === 'closed' && (
            <p className="rise inline-flex items-center gap-3 rounded-full bg-white/10 px-6 py-2 font-display text-[1.35em] font-bold text-[#fbf6ec] ring-1 ring-[rgba(232,210,156,0.55)] max-sm:gap-2 max-sm:px-4 max-sm:py-1 max-sm:text-[1.3em]">
              <span aria-hidden className="size-2.5 shrink-0 rounded-full bg-[#e8d29c]" />
              {phase.timedOut ? 'Vaxt bitdi. Növbəti sual…' : 'Cavab qeydə alındı. Növbəti sual…'}
            </p>
          )}
          {phase.kind === 'sending' && <p className="text-[#c2c7d3]">Göndərilir…</p>}
          {phase.kind === 'retrying' && (
            <p role="alert" className={alertClass}>{alertIcon}{sendError}</p>
          )}
          {phase.kind === 'open' && sendError && (
            <p role="alert" className={alertClass}>{alertIcon}{sendError}</p>
          )}
          {phase.kind === 'open' && !sendError && (
            <p className="text-[#9ea6b8]">
              Hər sual üçün {total} saniyə. Keçid üçün ən azı{' '}
              <span className="font-semibold tabular-nums text-[#e8d29c]">{state.passingScore}</span> / {state.totalQuestions} düzgün cavab
              lazımdır. Nəticə sonda açıqlanır.
            </p>
          )}
        </footer>
      </section>
    </main>
  )
}
