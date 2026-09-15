import { useEffect, useRef, useState } from 'react'
import { Navigate } from 'react-router-dom'
import AnswerButton, { type AnswerVisual } from '../components/AnswerButton'
import CarpetFrame from '../components/national/CarpetFrame'
import { Buta } from '../components/national/Ornaments'
import GameResult from './GameResult'
import SessionExpired from './SessionExpired'
import { useGame } from '../game/GameContext'
import { SessionInvalidError } from '../game/errors'
import { ANSWER_OPTIONS, optionText, type AnswerOption, type AnswerResult } from '../types/game'

const TRANSITION_MS = 1100
const TIMEOUT_RETRY_MS = 2000
// Answer taps are ignored this long after a question appears, so a stray second tap from the
// previous screen (e.g. a double tap on "Yenidən oyna") cannot answer the first question.
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

  // Visual countdown only: it mirrors the backend deadline; the backend decides lateness.
  useEffect(() => {
    if (state.status !== 'playing') return
    const id = window.setInterval(() => setNow(Date.now()), 200)
    return () => window.clearInterval(id)
  }, [state.status])

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

  function visualFor(option: AnswerOption): AnswerVisual {
    if (option === selected) return 'selected'
    return locked ? 'dimmed' : 'idle'
  }

  return (
    <main className="kiosk paper flex flex-col">
      <CarpetFrame />
      <section
        key={q.id}
        className="rise relative z-0 mx-auto flex min-h-0 w-full max-w-[110rem] flex-1 flex-col"
        style={{ padding: 'calc(var(--frame) + 0.9rem) calc(var(--frame) + 1.5rem) calc(var(--frame) + 0.8rem)' }}
      >
        <header className="flex items-center justify-between gap-6 text-[clamp(1rem,1.4vw,1.4rem)]">
          <div className="flex items-center gap-3">
            <Buta className="h-7 w-5" flip />
            <p className="font-display text-[1.7em] font-bold text-[var(--p-burgundy)]">
              Sual {state.questionNumber} / {state.totalQuestions}
            </p>
            <Buta className="h-7 w-5" />
          </div>
          <div
            role="timer"
            aria-live={urgent ? 'assertive' : 'off'}
            aria-label={`Qalan vaxt ${remainingSec} saniyə`}
            data-urgent={urgent ? 'true' : 'false'}
            className={[
              'relative grid h-[clamp(4.5rem,7vw,6rem)] w-[clamp(4.5rem,7vw,6rem)] shrink-0 place-items-center rounded-full bg-white font-display text-[clamp(1.8rem,2.8vw,2.7rem)] font-bold tabular-nums leading-none shadow-[var(--p-shadow)] transition-colors',
              remainingMs <= 0 || urgent ? 'text-[#b32a31]' : 'text-[var(--p-indigo)]',
              urgent && remainingMs > 0 ? 'motion-safe:animate-pulse' : '',
            ].join(' ')}
          >
            <svg viewBox="0 0 100 100" className="absolute inset-0 h-full w-full -rotate-90" aria-hidden>
              <circle cx="50" cy="50" r="44" fill="none" stroke="var(--p-line)" strokeWidth="7" />
              <circle
                cx="50" cy="50" r="44" fill="none"
                stroke={urgent ? '#b32a31' : 'var(--p-gold)'} strokeWidth="7" strokeLinecap="round"
                strokeDasharray={2 * Math.PI * 44}
                strokeDashoffset={2 * Math.PI * 44 * (1 - Math.min(1, remainingMs / (total * 1000)))}
              />
            </svg>
            <span data-testid="countdown" className="relative">{remainingSec}</span>
          </div>
        </header>

        <div className="relative my-3 flex min-h-0 flex-1">
          <Buta className="absolute -left-2 -top-3 z-10 h-10 w-7" flip />
          <Buta className="absolute -right-2 -top-3 z-10 h-10 w-7" />
          <Buta className="absolute -bottom-3 -left-2 z-10 h-10 w-7 rotate-180" />
          <Buta className="absolute -bottom-3 -right-2 z-10 h-10 w-7 rotate-180" flip />
          <div className={`flex min-h-0 w-full flex-1 rounded-xl border-[3px] border-[var(--p-gold)] bg-white px-[clamp(1.5rem,3vw,3.5rem)] py-4 shadow-[var(--p-shadow)] outline outline-1 outline-offset-[-9px] outline-[var(--p-gold-light)] ${hasImage ? 'flex-col items-center gap-5 lg:flex-row lg:items-center lg:gap-10' : 'flex-col justify-center'}`}>
          <h1
            lang="az"
            className={`max-w-[28ch] font-display font-semibold leading-[1.18] text-[var(--p-ink)] [overflow-wrap:anywhere] ${hasImage ? 'text-[clamp(1.6rem,2.6vw,3rem)] lg:flex-1' : 'text-[clamp(1.9rem,3.2vw,3.6rem)]'}`}
          >
            {q.text}
          </h1>
          {hasImage && (
            <figure className="flex h-[clamp(11rem,30vh,24rem)] w-full max-w-[44rem] shrink-0 items-center justify-center lg:w-[clamp(20rem,32vw,42rem)]" data-testid="question-image">
              {imageFailed ? (
                <p role="img" aria-label={q.imageAltText ?? 'Təsvir'} className="rounded-xl border-2 border-[var(--p-line)] bg-[var(--p-paper-2)] px-8 py-6 text-[clamp(1rem,1.3vw,1.3rem)] text-[var(--p-ink-2)]">
                  Təsvir yüklənmədi
                </p>
              ) : (
                <img
                  src={q.imageUrl ?? undefined}
                  alt={q.imageAltText ?? ''}
                  onError={() => setImageFailed(true)}
                  className="max-h-full max-w-full rounded-lg border-[3px] border-[var(--p-gold-light)] object-contain shadow-[var(--p-shadow)]"
                />
              )}
            </figure>
          )}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 lg:gap-6">
          {ANSWER_OPTIONS.map((o) => (
            <AnswerButton key={o} option={o} text={optionText(q, o)} visual={visualFor(o)} disabled={locked} onSelect={choose} />
          ))}
        </div>

        <footer className="mt-5 min-h-[5.5rem] text-[clamp(1rem,1.35vw,1.35rem)]" aria-live="polite">
          {phase.kind === 'closed' && (
            <div className="rise rounded-2xl border-2 border-[var(--p-gold-light)] bg-white px-5 py-4 shadow-[var(--p-shadow)]">
              <p className="font-display text-[1.5em] font-bold text-[var(--p-burgundy)]">
                {phase.timedOut ? 'Vaxt bitdi. Növbəti sual…' : 'Cavab qeydə alındı. Növbəti sual…'}
              </p>
            </div>
          )}
          {(phase.kind === 'sending' || phase.kind === 'retrying') && (
            <p className="px-1 text-[var(--p-ink-2)]">{phase.kind === 'retrying' ? sendError : 'Göndərilir…'}</p>
          )}
          {phase.kind === 'open' && sendError && (
            <p role="alert" className="rounded-2xl border-2 border-[#b32a31] bg-white px-5 py-4 text-[var(--p-ink)]">{sendError}</p>
          )}
          {phase.kind === 'open' && !sendError && (
            <p className="px-1 text-[var(--p-ink-2)]">
              Hər sual üçün {total} saniyə. Keçid üçün ən azı{' '}
              <span className="tabular-nums text-[var(--p-burgundy)] font-semibold">{state.passingScore}</span> / {state.totalQuestions} düzgün cavab
              lazımdır. Nəticə sonda açıqlanır.
            </p>
          )}
        </footer>
      </section>
    </main>
  )
}
