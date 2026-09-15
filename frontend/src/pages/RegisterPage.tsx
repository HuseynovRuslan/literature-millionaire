import { useRef, useState, type FormEvent, type InputHTMLAttributes, type RefObject } from 'react'
import { useNavigate } from 'react-router-dom'
import { PlayIcon, RuleBadge } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import { useGame } from '../game/GameContext'

/** Mirrors the backend rule: Azerbaijani mobile in 0XX…, 994XX… or +994XX… form (spaces and dashes allowed). */
const PHONE = /^(?:\+?994|0)(?:10|50|51|55|60|70|77|99)\d{7}$/
const compact = (v: string) => v.replace(/[\s\-()]/g, '')

const NAME_MESSAGE = 'Ad və soyadınızı yazın (ən azı 2 hərf).'
const PHONE_MESSAGE = 'Telefon nömrəsi düzgün deyil. Nümunə: 050 123 45 67'

type FieldErrors = { fullName: string | null; phone: string | null }
const NO_ERRORS: FieldErrors = { fullName: null, phone: null }

const STAGE =
  'home-stage rise rounded-[clamp(1.2rem,1.6vw,2rem)] px-[clamp(2rem,3.6vw,5rem)] py-[clamp(1.4rem,3.4vh,3.2rem)] max-lg:px-8 max-lg:py-7 max-sm:rounded-2xl max-sm:px-4 max-sm:py-5'

/** Label above a large touch input; the field's own error sits right under it and is linked for screen readers. */
function Field({
  id,
  label,
  error,
  inputRef,
  ...input
}: { id: string; label: string; error: string | null; inputRef: RefObject<HTMLInputElement | null> } & InputHTMLAttributes<HTMLInputElement>) {
  const errorId = `${id}-error`
  return (
    <div className="flex min-w-0 flex-col gap-[clamp(0.4rem,0.9vh,0.7rem)]">
      <label htmlFor={id} className="font-display text-[clamp(1.35rem,1.8vw,2rem)] font-semibold leading-none text-[#fbf6ec] max-sm:text-[1.15rem]">
        {label}
      </label>
      <input
        id={id}
        ref={inputRef}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className="gs-input min-h-[clamp(4.5rem,8vh,5.5rem)] w-full rounded-2xl border-2 px-[clamp(1.2rem,1.6vw,1.8rem)] font-sans text-[clamp(1.35rem,1.9vw,2.1rem)] max-sm:min-h-[3.5rem] max-sm:rounded-xl max-sm:px-4 max-sm:text-[1.2rem]"
        {...input}
      />
      {error && (
        <p id={errorId} className="flex items-start gap-2 text-[clamp(1rem,1.2vw,1.3rem)] font-medium leading-snug text-[#ffd0d3] max-sm:text-[0.95rem]">
          <span aria-hidden className="mt-[0.1em] grid size-[1.3em] shrink-0 place-items-center rounded-full bg-[#ffd0d3] text-[0.8em] font-bold text-[#5c0e18]">!</span>
          {error}
        </p>
      )}
    </div>
  )
}

/**
 * Touch registration before a quiz. Name and phone live only in this component's state and
 * are sent once with the start request; nothing is written to sessionStorage.
 */
export default function RegisterPage() {
  const navigate = useNavigate()
  const { state, startGame, reset, error, errorCode } = useGame()
  const starting = state.status === 'starting'
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>(NO_ERRORS)
  const nameRef = useRef<HTMLInputElement>(null)
  const phoneRef = useRef<HTMLInputElement>(null)
  // One start per registration. The context only ignores taps while a request is in flight; once it
  // succeeds, the form is briefly interactive again before navigation. With one attempt per campaign
  // a second tap in that window would get a 409 and wipe the quiz that was just started.
  const submittedRef = useRef(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (starting || submittedRef.current) return
    const name = fullName.trim().replace(/\s+/g, ' ')
    // Same rules and messages as before; each field now shows its own message under the field.
    const errors: FieldErrors = {
      fullName: name.length < 2 ? NAME_MESSAGE : null,
      phone: PHONE.test(compact(phone)) ? null : PHONE_MESSAGE,
    }
    setFieldErrors(errors)
    if (errors.fullName || errors.phone) {
      ;(errors.fullName ? nameRef : phoneRef).current?.focus()
      return
    }
    submittedRef.current = true
    const ok = await startGame({ fullName: name, phoneNumber: compact(phone) })
    if (ok) navigate('/game')
    else submittedRef.current = false // a failed start (network, validation) may be retried
  }

  function goHome() {
    reset()
    navigate('/')
  }

  if (errorCode === 'ATTEMPT_LIMIT_REACHED') {
    return (
      <GameShowShell>
        <section role="alert" data-testid="attempt-limit" className={`${STAGE} flex min-h-[clamp(20rem,52vh,36rem)] flex-col items-center justify-center text-center max-sm:min-h-[22rem]`}>
          <RuleBadge className="size-[clamp(5.5rem,10vh,8rem)] max-sm:size-20" />
          <h1 className="mt-5 max-w-[22ch] font-display text-[clamp(2.6rem,4.6vw,5.2rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[2rem]">
            İştirak hüququ istifadə olunub
          </h1>
          <p className="mt-4 max-w-[46rem] text-[clamp(1.15rem,1.7vw,1.8rem)] leading-relaxed text-[#d6deec] max-sm:text-base">
            {error ?? 'Bu kampaniyada artıq iştirak etmisiniz.'}
          </p>
          <p className="mt-3 font-display text-[clamp(1.3rem,1.9vw,2.1rem)] font-semibold text-[var(--p-gold-light)] max-sm:text-[1.15rem]">
            Növbəti bilik yarışında sizi yenidən gözləyirik.
          </p>
        </section>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="register-actions">
          <button type="button" onClick={goHome} className={`${PRIMARY_CTA} w-full max-w-[34rem]`} data-testid="limit-home">
            ANA SƏHİFƏ
          </button>
        </div>
      </GameShowShell>
    )
  }

  return (
    <GameShowShell>
      <form onSubmit={submit} noValidate aria-labelledby="register-title" className="flex flex-col gap-[clamp(1rem,2.6vh,2.2rem)] max-sm:gap-4" data-testid="register-form">
        <section
          data-testid="register-stage"
          className={`${STAGE} grid grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] items-center gap-x-[clamp(2rem,4.5vw,6rem)] max-lg:grid-cols-1 max-lg:gap-y-6 max-sm:gap-y-5`}
        >
          <div className="min-w-0">
            <h1 id="register-title" lang="az" className="font-display text-[clamp(3rem,min(4.8vw,9vh),6rem)] font-bold leading-[0.98] text-[#fbf6ec] [text-wrap:balance] max-sm:text-[2.2rem]">
              İştirakçı qeydiyyatı
            </h1>
            <p className="mt-[clamp(0.6rem,1.6vh,1.2rem)] max-w-[34ch] text-[clamp(1.15rem,1.6vw,1.8rem)] leading-snug text-[#d6deec] max-sm:text-base">
              Məlumatlarınızı daxil edin və bilik yarışına başlayın.
            </p>
            <div
              data-testid="attempt-rule"
              className="mt-[clamp(1rem,3vh,2.4rem)] flex max-w-[36rem] items-center gap-[clamp(0.8rem,1.2vw,1.2rem)] rounded-2xl bg-white/[0.07] px-[clamp(1rem,1.4vw,1.5rem)] py-[clamp(0.7rem,1.6vh,1.1rem)] ring-1 ring-[rgba(233,192,105,0.45)] max-sm:mt-4 max-sm:rounded-xl max-sm:px-3 max-sm:py-2.5"
            >
              <RuleBadge className="size-[clamp(3.2rem,4.4vw,4.4rem)] shrink-0 max-sm:size-11" />
              <p lang="az" className="text-[clamp(1.1rem,1.45vw,1.6rem)] font-semibold leading-snug text-[#fbf6ec] max-sm:text-[0.98rem]">
                Hər iştirakçı kampaniyada yalnız bir dəfə iştirak edə bilər.
              </p>
            </div>
          </div>

          <div className="flex min-w-0 flex-col gap-[clamp(1rem,2.4vh,1.8rem)] max-sm:gap-4">
            <Field
              id="register-full-name"
              label="Ad və soyad"
              error={fieldErrors.fullName}
              inputRef={nameRef}
              value={fullName}
              onChange={(e) => { setFullName(e.target.value); if (fieldErrors.fullName) setFieldErrors((f) => ({ ...f, fullName: null })) }}
              maxLength={120}
              autoComplete="off"
              autoCapitalize="words"
              spellCheck={false}
              enterKeyHint="next"
              placeholder="Məsələn: Ayşə Məmmədova"
              data-testid="fullName"
            />
            <Field
              id="register-phone"
              label="Telefon nömrəsi"
              error={fieldErrors.phone}
              inputRef={phoneRef}
              value={phone}
              onChange={(e) => { setPhone(e.target.value); if (fieldErrors.phone) setFieldErrors((f) => ({ ...f, phone: null })) }}
              type="tel"
              inputMode="tel"
              autoComplete="off"
              maxLength={20}
              enterKeyHint="go"
              placeholder="050 123 45 67"
              data-testid="phone"
            />
            {error && (
              <p
                role="alert"
                data-testid="register-server-error"
                className="flex items-start gap-3 rounded-2xl bg-[rgba(125,22,29,0.6)] px-5 py-4 text-[clamp(1.05rem,1.3vw,1.4rem)] font-medium leading-snug text-[#fbf6ec] ring-1 ring-[#ff9aa2] max-sm:rounded-xl max-sm:px-4 max-sm:py-3 max-sm:text-[0.98rem]"
              >
                <span aria-hidden className="grid size-[1.5em] shrink-0 place-items-center rounded-full bg-[#fbf6ec] font-bold text-[#7d161d]">!</span>
                <span>{error}</span>
              </p>
            )}
          </div>
        </section>

        <div data-testid="register-actions" className="rise flex w-full max-w-[78rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.5rem)] self-center [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
          <button type="submit" disabled={starting} aria-busy={starting} className={`${PRIMARY_CTA} flex-[1.7] disabled:opacity-70`} data-testid="register-submit">
            {starting ? 'Quiz hazırlanır…' : 'YARIŞA BAŞLA'}
            {!starting && <PlayIcon className="size-[0.8em] shrink-0" />}
          </button>
          <button type="button" onClick={goHome} disabled={starting} className={`${SECONDARY_CTA} flex-1 disabled:opacity-60`} data-testid="register-back">
            GERİ QAYIT
          </button>
        </div>
      </form>
    </GameShowShell>
  )
}
