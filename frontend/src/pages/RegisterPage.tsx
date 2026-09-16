import { useEffect, useRef, useState, type FormEvent, type InputHTMLAttributes, type RefObject } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { getAvailableCampaigns } from '../api/campaigns'
import QuizModeIcon from '../components/home/QuizModeIcon'
import { PlayIcon, RuleBadge } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import { useGame } from '../game/GameContext'
import type { CampaignSummary } from '../types/campaign'
import { formatDateRange } from '../utils/date'

/** Mirrors the backend rule: Azerbaijani mobile in 0XX…, 994XX… or +994XX… form (spaces and dashes allowed). */
const PHONE = /^(?:\+?994|0)(?:10|50|51|55|60|70|77|99)\d{7}$/
const compact = (v: string) => v.replace(/[\s\-()]/g, '')

const NAME_MESSAGE = 'Ad və soyadınızı yazın (ən azı 2 hərf).'
const PHONE_MESSAGE = 'Telefon nömrəsi düzgün deyil. Nümunə: 050 123 45 67'

type FieldErrors = { fullName: string | null; phone: string | null }
const NO_ERRORS: FieldErrors = { fullName: null, phone: null }

const STAGE =
  'home-stage rise rounded-[clamp(1.2rem,1.6vw,1.44rem)] px-[clamp(2rem,3.6vw,3.24rem)] py-[1.8063rem] max-lg:px-8 max-lg:py-7 max-sm:rounded-2xl max-sm:px-4 max-sm:py-5'

function parseCampaignId(value: string | undefined): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const parsed = Number(value)
  return Number.isSafeInteger(parsed) ? parsed : null
}

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
    <div className="flex min-w-0 flex-col gap-[0.4781rem]">
      <label htmlFor={id} className="font-display text-[clamp(1.35rem,1.8vw,1.62rem)] font-semibold leading-none text-[#fbf6ec] max-sm:text-[1.15rem]">
        {label}
      </label>
      <input
        id={id}
        ref={inputRef}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className="gs-input min-h-[4.5rem] w-full rounded-2xl border-2 px-[clamp(1.2rem,1.6vw,1.44rem)] font-sans text-[clamp(1.35rem,1.9vw,1.71rem)] max-sm:min-h-[3.5rem] max-sm:rounded-xl max-sm:px-4 max-sm:text-[1.2rem]"
        {...input}
      />
      {error && (
        <p id={errorId} className="flex items-start gap-2 text-[clamp(1rem,1.2vw,1.08rem)] font-medium leading-snug text-[#ffd0d3] max-sm:text-[0.95rem]">
          <span aria-hidden className="mt-[0.1em] grid size-[1.3em] shrink-0 place-items-center rounded-full bg-[#ffd0d3] text-[0.8em] font-bold text-[#5c0e18]">!</span>
          {error}
        </p>
      )}
    </div>
  )
}

type CampaignLookup = { kind: 'loading' } | { kind: 'found'; campaign: CampaignSummary } | { kind: 'not-found' }

/**
 * Touch registration before a quiz, for the category selected on the home screen (route
 * /register/:campaignId). Name and phone live only in this component's state and are sent once
 * with the start request; nothing is written to sessionStorage. campaignId is only a selection
 * identifier - every quiz rule (passing score, image count, book) still comes from the backend.
 */
export default function RegisterPage() {
  const navigate = useNavigate()
  const { campaignId: routeCampaignId } = useParams()
  const campaignId = parseCampaignId(routeCampaignId)
  const { state, startGame, reset, error, errorCode } = useGame()
  const starting = state.status === 'starting'
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>(NO_ERRORS)
  const [lookup, setLookup] = useState<CampaignLookup>({ kind: 'loading' })
  const nameRef = useRef<HTMLInputElement>(null)
  const phoneRef = useRef<HTMLInputElement>(null)
  // One start per registration. The context only ignores taps while a request is in flight; once it
  // succeeds, the form is briefly interactive again before navigation. With one attempt per campaign
  // a second tap in that window would get a 409 and wipe the quiz that was just started.
  const submittedRef = useRef(false)

  // The selected category must still be playable: /api/campaigns/available is the only source of
  // category identity, so a stale, disabled or unknown campaignId is caught here, before registration.
  useEffect(() => {
    if (campaignId === null) return
    let cancelled = false
    getAvailableCampaigns()
      .then((data) => {
        if (cancelled) return
        const found = data.find((c) => c.campaignId === campaignId)
        setLookup(found ? { kind: 'found', campaign: found } : { kind: 'not-found' })
      })
      .catch(() => { if (!cancelled) setLookup({ kind: 'not-found' }) })
    return () => { cancelled = true }
  }, [campaignId])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (starting || submittedRef.current || campaignId === null) return
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
    const ok = await startGame({ fullName: name, phoneNumber: compact(phone), campaignId })
    if (ok) navigate('/game')
    else submittedRef.current = false // a failed start (network, validation) may be retried
  }

  function goHome() {
    reset()
    navigate('/')
  }

  // No campaignId in the route, or it no longer names a playable category: never silently fall back
  // to a default campaign. Safe redirect message, back to category selection.
  if (campaignId === null || lookup.kind === 'not-found') {
    return (
      <GameShowShell>
        <section role="alert" data-testid="register-invalid" className={`${STAGE} flex min-h-[27.625rem] flex-col items-center justify-center text-center max-sm:min-h-[22rem]`}>
          <RuleBadge className="size-[5.5rem] max-sm:size-20" />
          <h1 className="mt-5 max-w-[24ch] font-display text-[clamp(2.2rem,3.8vw,3.42rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[1.9rem]">
            Bu kateqoriya artıq mövcud deyil
          </h1>
          <p className="mt-4 max-w-[44rem] text-[clamp(1.1rem,1.6vw,1.44rem)] leading-relaxed text-[#d6deec] max-sm:text-base">
            Zəhmət olmasa, kateqoriya seçimi ekranından yenidən seçin.
          </p>
        </section>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="register-actions">
          <button type="button" onClick={goHome} className={`${PRIMARY_CTA} w-full max-w-[34rem]`} data-testid="register-back-home">
            KATEQORİYALARA QAYIT
          </button>
        </div>
      </GameShowShell>
    )
  }

  if (lookup.kind === 'loading') {
    return (
      <GameShowShell>
        <section role="status" aria-live="polite" data-testid="register-loading" className={`${STAGE} flex min-h-[27.625rem] flex-col items-center justify-center text-center max-sm:min-h-[22rem]`}>
          <span className="spin inline-block h-12 w-12 rounded-full border-4 border-white/20 border-t-[var(--p-gold-light)]" aria-hidden />
          <p className="mt-5 font-display text-[clamp(1.6rem,2.6vw,2.34rem)] font-semibold text-[#fbf6ec] max-sm:text-[1.4rem]">Kateqoriya yoxlanılır…</p>
        </section>
      </GameShowShell>
    )
  }

  const { campaign } = lookup

  if (errorCode === 'ATTEMPT_LIMIT_REACHED') {
    return (
      <GameShowShell>
        <section role="alert" data-testid="attempt-limit" className={`${STAGE} flex min-h-[27.625rem] flex-col items-center justify-center text-center max-sm:min-h-[22rem]`}>
          <RuleBadge className="size-[5.5rem] max-sm:size-20" />
          <h1 className="mt-5 max-w-[22ch] font-display text-[clamp(2.6rem,4.6vw,4.14rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[2rem]">
            İştirak hüququ istifadə olunub
          </h1>
          <p className="mt-3 font-display text-[clamp(1.2rem,1.7vw,1.53rem)] font-semibold text-[var(--p-gold-light)] max-sm:text-[1.05rem]">
            {campaign.quizMode.title}
          </p>
          <p className="mt-4 max-w-[46rem] text-[clamp(1.15rem,1.7vw,1.53rem)] leading-relaxed text-[#d6deec] max-sm:text-base">
            {error ?? 'Bu kampaniyada artıq iştirak etmisiniz.'}
          </p>
          <p className="mt-3 font-display text-[clamp(1.3rem,1.9vw,1.71rem)] font-semibold text-[var(--p-gold-light)] max-sm:text-[1.15rem]">
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
      <form onSubmit={submit} noValidate aria-labelledby="register-title" className="flex flex-col gap-[1.3813rem] max-sm:gap-4" data-testid="register-form">
        <section
          data-testid="register-stage"
          className={`${STAGE} grid grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] items-center gap-x-[clamp(2rem,4.5vw,4.05rem)] max-lg:grid-cols-1 max-lg:gap-y-6 max-sm:gap-y-5`}
        >
          <div className="min-w-0">
            <div className="flex items-center gap-3" data-testid="register-category">
              <span aria-hidden="true" className="grid size-[clamp(2.6rem,3.4vw,3.06rem)] shrink-0 place-items-center rounded-full bg-[rgba(243,215,126,0.14)] text-[var(--p-gold-light)] ring-1 ring-[rgba(233,192,105,0.55)]">
                <QuizModeIcon iconKey={campaign.quizMode.iconKey} className="size-[56%]" />
              </span>
              <p lang="az" className="min-w-0 truncate font-display text-[clamp(1.15rem,1.5vw,1.35rem)] font-semibold uppercase tracking-[0.1em] text-[var(--p-gold-light)] max-sm:text-[0.95rem]">
                {campaign.quizMode.title}
              </p>
            </div>
            <h1 id="register-title" lang="az" className="mt-[0.5312rem] font-display text-[clamp(2.6rem,4.4vw,3.96rem)] font-bold leading-[0.98] text-[#fbf6ec] [text-wrap:balance] max-sm:text-[2rem]">
              İştirakçı qeydiyyatı
            </h1>
            {campaign.book && (
              <p lang="az" className="mt-[0.425rem] max-w-[36ch] text-[clamp(1rem,1.3vw,1.17rem)] text-[#c9d3e6] max-sm:text-[0.9rem]">
                Kitab: {campaign.book.title}
                {campaign.book.author.trim() ? ` — ${campaign.book.author.trim()}` : ''}
              </p>
            )}
            <p className="mt-[0.425rem] text-[clamp(0.95rem,1.2vw,1.08rem)] text-[#aab8d4] max-sm:text-[0.85rem]">
              {formatDateRange(campaign.startDate, campaign.endDate)}
            </p>
            <p className="mt-[0.85rem] max-w-[34ch] text-[clamp(1.15rem,1.6vw,1.44rem)] leading-snug text-[#d6deec] max-sm:text-base">
              Məlumatlarınızı daxil edin və bilik yarışına başlayın.
            </p>
            <div
              data-testid="attempt-rule"
              className="mt-[1.5938rem] flex max-w-[36rem] items-center gap-[clamp(0.8rem,1.2vw,1.08rem)] rounded-2xl bg-white/[0.07] px-[clamp(1rem,1.4vw,1.26rem)] py-[0.85rem] ring-1 ring-[rgba(233,192,105,0.45)] max-sm:mt-4 max-sm:rounded-xl max-sm:px-3 max-sm:py-2.5"
            >
              <RuleBadge className="size-[clamp(3.2rem,4.4vw,3.96rem)] shrink-0 max-sm:size-11" />
              <p lang="az" className="text-[clamp(1.1rem,1.45vw,1.305rem)] font-semibold leading-snug text-[#fbf6ec] max-sm:text-[0.98rem]">
                Hər iştirakçı kampaniyada yalnız bir dəfə iştirak edə bilər.
              </p>
            </div>
          </div>

          <div className="flex min-w-0 flex-col gap-[1.275rem] max-sm:gap-4">
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
                className="flex items-start gap-3 rounded-2xl bg-[rgba(125,22,29,0.6)] px-5 py-4 text-[clamp(1.05rem,1.3vw,1.17rem)] font-medium leading-snug text-[#fbf6ec] ring-1 ring-[#ff9aa2] max-sm:rounded-xl max-sm:px-4 max-sm:py-3 max-sm:text-[0.98rem]"
              >
                <span aria-hidden className="grid size-[1.5em] shrink-0 place-items-center rounded-full bg-[#fbf6ec] font-bold text-[#7d161d]">!</span>
                <span>{error}</span>
              </p>
            )}
          </div>
        </section>

        <div data-testid="register-actions" className="rise flex w-full max-w-[78rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.26rem)] self-center [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
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
