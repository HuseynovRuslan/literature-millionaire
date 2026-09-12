import { useEffect, useRef, useState } from 'react'
import { Navigate } from 'react-router-dom'
import AnswerButton, { type AnswerVisual } from '../components/AnswerButton'
import GameResult from './GameResult'
import SessionExpired from './SessionExpired'
import { useGame } from '../game/GameContext'
import { SessionInvalidError } from '../game/errors'
import { ANSWER_OPTIONS, optionText, type AnswerOption, type AnswerResult } from '../types/game'

const TRANSITION_MS = 1100
const TIMEOUT_RETRY_MS = 2000

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

  const q = state.question
  const deadline = state.questionExpiresAtUtc ? Date.parse(state.questionExpiresAtUtc) : null
  const remainingMs = deadline === null ? Infinity : Math.max(0, deadline - now)
  const remainingSec = Math.ceil(remainingMs / 1000)
  const total = state.secondsPerQuestion
  // Warning window scales with the question time: 5 s of 15, 10 s of 30.
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
  }, [q?.id])

  function scheduleAdvance(result: AnswerResult) {
    timer.current = window.setTimeout(() => advance(result), TRANSITION_MS)
  }

  async function choose(option: AnswerOption) {
    if (phase.kind !== 'open' || inFlight.current || remainingMs <= 0) return
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
    <main className="kiosk ornament flex flex-col p-6 lg:p-8">
      <section key={q.id} className="rise mx-auto flex min-h-0 w-full max-w-[110rem] flex-1 flex-col">
        <header className="flex items-center justify-between gap-6 text-[clamp(1rem,1.4vw,1.4rem)]">
          <p className="font-display text-[1.6em] font-semibold text-gold-light">
            Sual {state.questionNumber} / {state.totalQuestions}
          </p>
          <div
            role="timer"
            aria-live={urgent ? 'assertive' : 'off'}
            aria-label={`Qalan vaxt ${remainingSec} saniyə`}
            data-urgent={urgent ? 'true' : 'false'}
            className={[
              'flex items-center gap-3 rounded-full border-2 px-5 py-1.5 font-display text-[1.9em] font-bold tabular-nums leading-none transition-colors',
              remainingMs <= 0
                ? 'border-bad bg-bad/20 text-bad'
                : urgent
                  ? 'border-bad bg-bad/15 text-bad motion-safe:animate-pulse'
                  : 'border-gold/60 bg-navy-800/70 text-gold-light',
            ].join(' ')}
          >
            <span className="font-sans text-[0.55em] font-medium text-mist">Vaxt</span>
            <span data-testid="countdown">{remainingSec}</span>
            <span className="h-2 w-28 overflow-hidden rounded-full bg-navy-600/70" aria-hidden>
              <span
                className={`block h-full rounded-full ${urgent ? 'bg-bad' : 'bg-gold'}`}
                style={{ width: `${Math.min(100, (remainingMs / (total * 1000)) * 100)}%` }}
              />
            </span>
          </div>
        </header>

        <div className={`flex min-h-0 flex-1 py-4 ${hasImage ? 'flex-col items-center gap-6 lg:flex-row lg:items-center lg:gap-12' : 'flex-col justify-center'}`}>
          <h1
            lang="az"
            className={`max-w-[28ch] font-display font-semibold leading-[1.18] text-ivory [overflow-wrap:anywhere] ${hasImage ? 'text-[clamp(1.6rem,2.6vw,3rem)] lg:flex-1' : 'text-[clamp(1.9rem,3.2vw,3.6rem)]'}`}
          >
            {q.text}
          </h1>
          {hasImage && (
            <figure className="flex h-[clamp(12rem,34vh,26rem)] w-full max-w-[44rem] shrink-0 items-center justify-center lg:w-[clamp(20rem,34vw,44rem)]" data-testid="question-image">
              {imageFailed ? (
                <p role="img" aria-label={q.imageAltText ?? 'Təsvir'} className="rounded-2xl border border-navy-600/70 bg-navy-800/60 px-8 py-6 text-[clamp(1rem,1.3vw,1.3rem)] text-mist">
                  Təsvir yüklənmədi
                </p>
              ) : (
                <img
                  src={q.imageUrl ?? undefined}
                  alt={q.imageAltText ?? ''}
                  onError={() => setImageFailed(true)}
                  className="max-h-full max-w-full rounded-2xl object-contain shadow-[0_24px_50px_-24px_rgba(0,0,0,0.8)]"
                />
              )}
            </figure>
          )}
        </div>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 lg:gap-6">
          {ANSWER_OPTIONS.map((o) => (
            <AnswerButton key={o} option={o} text={optionText(q, o)} visual={visualFor(o)} disabled={locked} onSelect={choose} />
          ))}
        </div>

        <footer className="mt-5 min-h-[5.5rem] text-[clamp(1rem,1.35vw,1.35rem)]" aria-live="polite">
          {phase.kind === 'closed' && (
            <div className="rise rounded-2xl border border-gold/50 bg-navy-800/70 px-5 py-4">
              <p className="font-display text-[1.5em] font-bold text-gold-light">
                {phase.timedOut ? 'Vaxt bitdi. Növbəti sual…' : 'Cavab qeydə alındı. Növbəti sual…'}
              </p>
            </div>
          )}
          {(phase.kind === 'sending' || phase.kind === 'retrying') && (
            <p className="px-1 text-mist/80">{phase.kind === 'retrying' ? sendError : 'Göndərilir…'}</p>
          )}
          {phase.kind === 'open' && sendError && (
            <p role="alert" className="rounded-2xl border border-bad/60 bg-bad/10 px-5 py-4 text-ivory">{sendError}</p>
          )}
          {phase.kind === 'open' && !sendError && (
            <p className="px-1 text-mist/80">
              Hər sual üçün {total} saniyə. Keçid üçün ən azı{' '}
              <span className="tabular-nums text-gold-light">{state.passingScore}</span> / {state.totalQuestions} düzgün cavab
              lazımdır. Nəticə sonda açıqlanır.
            </p>
          )}
        </footer>
      </section>
    </main>
  )
}
