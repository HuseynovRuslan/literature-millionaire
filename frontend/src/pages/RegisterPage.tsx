import { useEffect, useRef, useState, type CSSProperties, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { getAvailableCampaigns } from '../api/campaigns'
import QuizModeIcon from '../components/home/QuizModeIcon'
import { accentFor } from '../components/home/categoryThemes'
import { ArrowLeftIcon, CheckIcon, ClockIcon, ListIcon, PhoneIcon, PlayIcon, RewardMedal, RuleBadge, TargetIcon, UserIcon } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import { Octagram } from '../components/arena/NationalMotifs'
import GameShowShell from '../components/home/GameShowShell'
import QrCode from '../components/QrCode'
import { useQrLogin } from '../hooks/useQrLogin'
import { useGame } from '../game/GameContext'
import type { CampaignSummary } from '../types/campaign'
import { formatDateRange } from '../utils/date'




const CARD = 'card rise rounded-[2rem] max-sm:rounded-3xl'

function parseCampaignId(value: string | undefined): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const parsed = Number(value)
  return Number.isSafeInteger(parsed) ? parsed : null
}

/** Where the participant is in the flow: category chosen, registering now, the quiz next. */
function Stepper() {
  const steps = ['Kateqoriya', 'Qeydiyyat', 'Yarış']
  return (
    <ol className="rise flex items-center justify-center gap-2 max-sm:gap-1.5" aria-label="Addımlar">
      {steps.map((label, i) => {
        const done = i === 0
        const current = i === 1
        return (
          <li key={label} className="flex items-center gap-2 max-sm:gap-1.5" aria-current={current ? 'step' : undefined}>
            <span
              className={`grid size-9 place-items-center rounded-full font-display text-sm font-extrabold max-sm:size-7 max-sm:text-xs ${
                done ? 'pop bg-ok text-ink-950' : current ? 'glow-pulse bg-sun text-ink-950' : 'bg-white/10 text-fg-3'
              }`}
            >
              {done ? <CheckIcon className="size-4" /> : i + 1}
            </span>
            <span className={`text-[clamp(0.85rem,0.95vw,0.95rem)] font-bold max-sm:text-[0.78rem] ${current ? 'text-fg' : 'text-fg-3'}`}>{label}</span>
            {i < steps.length - 1 && <span aria-hidden className="mx-1 h-0.5 w-[clamp(1.5rem,4vw,3.5rem)] rounded-full bg-white/15 max-sm:w-4" />}
          </li>
        )
      })}
    </ol>
  )
}

type CampaignLookup = { kind: 'loading' } | { kind: 'found'; campaign: CampaignSummary } | { kind: 'not-found' }

/**
 * Touch registration before a quiz, for the category selected on the home screen (route
 * /register/:campaignId). Name and phone live only in this component's state and are sent once
 * with the start request; nothing is written to sessionStorage. campaignId is only a selection
 * identifier - every quiz rule (passing score, image count, book) still comes from the backend.
 */
/**
 * The way in. Most people reaching this screen are colleagues already carrying QRLog, so the QR is
 * the screen rather than something behind a button: it opens by itself and they scan it.
 *
 * Only the code is in the QR. The browser keeps a separate secret and polls with that, so the QR
 * being visible to the room gives nothing away - see the backend's IQrLoginService.
 */
function QrLoginPanel({ state, onRetry }: {
  state: ReturnType<typeof useQrLogin>['state']
  onRetry: () => void
}) {
  return (
    <div className="flex min-w-0 flex-col items-center text-center" data-testid="qrlog-panel" aria-live="polite">
      {/* The logo and the code share one white card: the QR needs a light background to scan, and on a
          dark studio screen a floating white square would read as a hole rather than as a sign-in. */}
      <div className="flex w-full max-w-[22rem] flex-col items-center gap-3 rounded-3xl bg-white p-[clamp(0.9rem,1.6vw,1.4rem)] shadow-[0_1rem_2.4rem_-0.8rem_rgba(0,0,0,0.55)]">
        <img
          src="/brand/qrlog-logo.webp"
          alt="QRLog"
          width={720}
          height={265}
          className="h-[clamp(1.6rem,2.4vw,2.2rem)] w-auto"
          data-testid="qrlog-logo"
        />

        {state.kind === 'waiting' ? (
          <QrCode
            value={state.qrValue}
            title="QRLog tətbiqi ilə oxutmaq üçün QR kod"
            className="aspect-square w-full max-w-[16rem]"
          />
        ) : (
          // Same square either way, so the card does not jump while a code is being minted or renewed.
          <div className="grid aspect-square w-full max-w-[16rem] place-items-center rounded-xl bg-ink-950/5 px-4 text-center">
            {state.kind === 'starting' || state.kind === 'idle' ? (
              <p className="font-semibold text-ink-950/60">QR kod hazırlanır…</p>
            ) : (
              <div>
                <p lang="az" className="font-bold text-ink-950/75">
                  {state.kind === 'expired' ? 'QR kodun vaxtı bitdi' : 'Əlaqə alınmadı'}
                </p>
                <button type="button" onClick={onRetry} className="tap mt-3 rounded-xl bg-brand px-5 py-2.5 font-display font-bold text-white" data-testid="qrlog-retry">
                  Yeni QR kod
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      <p lang="az" className="mt-4 font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-extrabold">
        QRLog tətbiqi ilə oxudun
      </p>
      <p lang="az" className="mt-1 max-w-[34ch] text-[clamp(0.9rem,1.05vw,1rem)] font-medium leading-snug text-fg-2">
        Telefonunuzda QRLog tətbiqini açın və kodu skan edin — adınız və nömrəniz özü gələcək.
      </p>
      {state.kind === 'waiting' && (
        <p className="mt-2 text-[clamp(0.8rem,0.9vw,0.88rem)] font-bold tabular-nums text-fg-3" data-testid="qrlog-countdown">
          Kodun vaxtı: {state.secondsLeft} saniyə
        </p>
      )}

    </div>
  )
}

/** After QRLog has vouched: who arrived, before a quiz starts under their name. */
function QrLoginWelcome({ fullName, phoneNumber }: { fullName: string; phoneNumber: string }) {
  return (
    <div className="pop flex min-w-0 flex-col items-center text-center" data-testid="qrlog-signed-in">
      <span className="grid size-16 place-items-center rounded-full bg-ok/15 text-ok ring-1 ring-ok/40 max-sm:size-14">
        <CheckIcon className="size-8 max-sm:size-7" />
      </span>
      <p lang="az" className="mt-4 font-display text-[clamp(1.4rem,2.2vw,2rem)] font-extrabold leading-tight">
        Xoş gəldiniz, dəyərli QRLog üzvü!
      </p>
      <p lang="az" className="mt-1 text-[clamp(0.95rem,1.1vw,1.05rem)] font-medium text-fg-2">
        Məlumatlarınız QRLog-dan gəldi. Yoxlayın və yarışa başlayın.
      </p>

      <dl className="mt-5 w-full max-w-[24rem] overflow-hidden rounded-2xl bg-white/[0.05] text-left ring-1 ring-white/10">
        <div className="flex items-center gap-3 px-4 py-3">
          <UserIcon className="size-5 shrink-0 text-brand-soft" />
          <div className="min-w-0">
            <dt className="text-[0.72rem] font-bold uppercase tracking-[0.12em] text-fg-3">Ad və soyad</dt>
            <dd lang="az" className="font-display text-[clamp(1.05rem,1.3vw,1.25rem)] font-bold [overflow-wrap:anywhere]" data-testid="qrlog-name">{fullName}</dd>
          </div>
        </div>
        <div className="flex items-center gap-3 border-t border-white/10 px-4 py-3">
          <PhoneIcon className="size-5 shrink-0 text-brand-soft" />
          <div className="min-w-0">
            <dt className="text-[0.72rem] font-bold uppercase tracking-[0.12em] text-fg-3">Telefon nömrəsi</dt>
            <dd className="font-display text-[clamp(1.05rem,1.3vw,1.25rem)] font-bold tabular-nums" data-testid="qrlog-phone">{phoneNumber}</dd>
          </div>
        </div>
      </dl>

    </div>
  )
}

export default function RegisterPage() {
  const navigate = useNavigate()
  const { campaignId: routeCampaignId } = useParams()
  const campaignId = parseCampaignId(routeCampaignId)
  const { state, startGame, reset, error, errorCode } = useGame()
  const starting = state.status === 'starting'
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [signedInAs, setSignedInAs] = useState<{ fullName: string; phoneNumber: string } | null>(null)
  const [lookup, setLookup] = useState<CampaignLookup>({ kind: 'loading' })

  // A confirmed QRLog sign-in fills the form rather than submitting it: on a shared kiosk the player
  // should see whose name landed there before the quiz starts under it.
  const qrLogin = useQrLogin(({ fullName: name, phoneNumber }) => {
    setFullName(name)
    setPhone(phoneNumber)
    setSignedInAs({ fullName: name, phoneNumber })
    // The player is looking at their phone when this lands. On a laptop window the start button sits
    // below the fold while the QR is up, so bring it into view rather than leave them to find it.
    requestAnimationFrame(() => submitRef.current?.scrollIntoView({
      block: 'nearest',
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
    }))
  })
  const submitRef = useRef<HTMLButtonElement>(null)
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

  // Open the sign-in as soon as there is a category to play: the QR is the screen, not a step behind
  // a button. Runs once - beginRef keeps a re-render from minting a second code and orphaning the
  // first, which the player may already be scanning.
  const beganRef = useRef(false)
  useEffect(() => {
    if (lookup.kind !== 'found' || beganRef.current) return
    beganRef.current = true
    void qrLogin.begin()
  }, [lookup.kind, qrLogin])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (starting || submittedRef.current || campaignId === null) return
    // Nothing here is typed any more: the name and phone came from QRLog, which is the only way in.
    // The server has the last word on both, and says so where the player can read it - a check here
    // that failed silently was what made the start button look broken.
    submittedRef.current = true
    const ok = await startGame({ fullName: fullName.trim(), phoneNumber: phone, campaignId })
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
        <section role="alert" data-testid="register-invalid" className={`${CARD} mx-auto flex min-h-[24rem] w-full max-w-[56rem] flex-col items-center justify-center px-8 py-12 text-center max-sm:min-h-[20rem] max-sm:px-5`}>
          <RuleBadge className="pop size-24 max-sm:size-20" />
          <h1 className="mt-6 max-w-[24ch] font-display text-[clamp(1.8rem,3.2vw,2.8rem)] font-bold leading-tight max-sm:text-[1.5rem]">
            Bu kateqoriya artıq mövcud deyil
          </h1>
          <p className="mt-3 max-w-[40rem] text-[clamp(1.05rem,1.4vw,1.3rem)] leading-relaxed text-fg-2 max-sm:text-base">
            Zəhmət olmasa, kateqoriya seçimi ekranından yenidən seçin.
          </p>
        </section>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="register-actions">
          <button type="button" onClick={goHome} className={`${PRIMARY_CTA} w-full max-w-[30rem]`} data-testid="register-back-home">
            Kateqoriyalara qayıt
          </button>
        </div>
      </GameShowShell>
    )
  }

  if (lookup.kind === 'loading') {
    return (
      <GameShowShell>
        <section role="status" aria-live="polite" data-testid="register-loading" className={`${CARD} mx-auto flex min-h-[24rem] w-full max-w-[56rem] flex-col items-center justify-center px-8 py-12 text-center max-sm:min-h-[20rem]`}>
          <Octagram className="spin size-12 text-sun" />
          <p className="mt-5 font-display text-[clamp(1.4rem,2.2vw,2rem)] font-bold max-sm:text-[1.3rem]">Kateqoriya yoxlanılır…</p>
        </section>
      </GameShowShell>
    )
  }

  const { campaign } = lookup

  if (errorCode === 'ATTEMPT_LIMIT_REACHED') {
    return (
      <GameShowShell>
        <section role="alert" data-testid="attempt-limit" className={`${CARD} card-glow mx-auto flex min-h-[24rem] w-full max-w-[56rem] flex-col items-center justify-center px-8 py-12 text-center max-sm:min-h-[20rem] max-sm:px-5`}>
          <RuleBadge className="pop size-24 max-sm:size-20" />
          <h1 className="mt-6 max-w-[22ch] font-display text-[clamp(2rem,3.8vw,3.2rem)] font-bold leading-tight max-sm:text-[1.6rem]">
            İştirak hüququ istifadə olunub
          </h1>
          <p className="chip mt-4 px-4 py-1.5 text-[clamp(0.85rem,1vw,1rem)] text-brand-soft">
            {campaign.quizMode.title}
          </p>
          <p className="mt-4 max-w-[44rem] text-[clamp(1.05rem,1.4vw,1.3rem)] leading-relaxed text-fg-2 max-sm:text-base">
            {error ?? 'Bu kampaniyada artıq iştirak etmisiniz.'}
          </p>
          <p className="mt-3 font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-bold text-sun max-sm:text-[1.05rem]">
            Növbəti bilik yarışında sizi yenidən gözləyirik.
          </p>
        </section>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="register-actions">
          <button type="button" onClick={goHome} className={`${PRIMARY_CTA} w-full max-w-[30rem]`} data-testid="limit-home">
            Ana səhifə
          </button>
        </div>
      </GameShowShell>
    )
  }

  return (
    <GameShowShell>
      <Stepper />
      <form onSubmit={submit} noValidate aria-labelledby="register-title" className="mx-auto flex w-full max-w-[84rem] flex-col gap-[clamp(1rem,2vh,1.5rem)] max-sm:gap-4" data-testid="register-form">
        <section data-testid="register-stage" className="grid grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] gap-[clamp(1rem,1.6vw,1.5rem)] max-lg:grid-cols-1">
          {/* Category summary */}
          <div className={`${CARD} flex min-w-0 flex-col overflow-hidden`}>
            <div
              style={{ '--accent': accentFor(campaign.quizMode.slug, 0) } as CSSProperties}
              className="flex items-center gap-4 bg-[linear-gradient(135deg,color-mix(in_srgb,var(--accent)_92%,#ffffff),color-mix(in_srgb,var(--accent)_72%,#110c2c))] px-[clamp(1.2rem,2vw,2rem)] py-5"
              data-testid="register-category"
            >
              <span aria-hidden="true" className="grid size-14 shrink-0 place-items-center rounded-2xl bg-[color-mix(in_srgb,var(--accent)_45%,#ffffff_28%)] text-white ring-1 ring-white/30 max-sm:size-12">
                <QuizModeIcon iconKey={campaign.quizMode.iconKey} className="size-[58%]" />
              </span>
              <div className="min-w-0">
                <p className="text-[0.8rem] font-bold uppercase tracking-[0.14em] text-white/80">Seçilmiş kateqoriya</p>
                <p lang="az" className="font-display text-[clamp(1.2rem,1.7vw,1.6rem)] font-bold leading-tight text-white">
                  {campaign.quizMode.title}
                </p>
              </div>
            </div>
            <div className="flex flex-1 flex-col gap-4 px-[clamp(1.2rem,2vw,2rem)] py-[clamp(1.2rem,2vh,1.6rem)]">
              {campaign.book && (
                <p lang="az" className="text-[clamp(0.98rem,1.1vw,1.08rem)] text-fg-2">
                  <span className="font-bold text-brand-soft">Kitab: </span>
                  {campaign.book.title}
                  {campaign.book.author.trim() ? ` — ${campaign.book.author.trim()}` : ''}
                </p>
              )}
              <ul className="grid grid-cols-2 gap-2.5 text-[clamp(0.9rem,1vw,1rem)]">
                <li className="flex items-center gap-2 rounded-xl bg-white/[0.05] px-3 py-2.5 font-bold text-fg-2 ring-1 ring-white/10"><ListIcon className="size-5 text-brand-soft" />{campaign.questionCount} sual</li>
                <li className="flex items-center gap-2 rounded-xl bg-white/[0.05] px-3 py-2.5 font-bold text-fg-2 ring-1 ring-white/10"><TargetIcon className="size-5 text-brand-soft" />Keçid: {campaign.passingScore}/{campaign.questionCount}</li>
                <li className="col-span-2 flex items-center gap-2 rounded-xl bg-white/[0.05] px-3 py-2.5 font-bold text-fg-2 ring-1 ring-white/10"><ClockIcon className="size-5 text-brand-soft" />{formatDateRange(campaign.startDate, campaign.endDate)}</li>
              </ul>
              {campaign.rewardTitle.trim() && (
                <p lang="az" className="flex items-center gap-3 rounded-2xl bg-sun/10 px-3.5 py-2.5 text-[clamp(0.95rem,1.05vw,1.05rem)] font-bold text-sun ring-1 ring-sun/25">
                  <RewardMedal className="size-8 shrink-0" />
                  Mükafat: {campaign.rewardTitle}
                </p>
              )}
              <div data-testid="attempt-rule" className="mt-auto flex items-center gap-3 rounded-2xl bg-brand/15 px-3.5 py-3 ring-1 ring-brand/40">
                <RuleBadge className="size-12 shrink-0 max-sm:size-10" />
                <p lang="az" className="text-[clamp(0.95rem,1.1vw,1.08rem)] font-bold leading-snug text-fg">
                  Hər iştirakçı kampaniyada yalnız bir dəfə iştirak edə bilər.
                </p>
              </div>
            </div>
          </div>

          {/* Form */}
          <div className={`${CARD} card-glow flex min-w-0 flex-col justify-center gap-[clamp(1rem,2vh,1.4rem)] px-[clamp(1.2rem,3vw,3rem)] py-[clamp(1.4rem,3vh,2.4rem)] [animation-delay:60ms]`}>
            {signedInAs ? (
              <QrLoginWelcome fullName={signedInAs.fullName} phoneNumber={signedInAs.phoneNumber} />
            ) : (
              <QrLoginPanel
                state={qrLogin.state}
                onRetry={() => { setSignedInAs(null); void qrLogin.begin() }}
              />
            )}
            {error && (
              <p
                role="alert"
                data-testid="register-server-error"
                className="flex items-start gap-3 rounded-2xl bg-bad/15 px-5 py-4 text-[clamp(1rem,1.2vw,1.1rem)] font-semibold leading-snug text-fg ring-1 ring-bad/60 max-sm:px-4 max-sm:py-3 max-sm:text-[0.95rem]"
              >
                <span aria-hidden className="grid size-[1.5em] shrink-0 place-items-center rounded-full bg-bad font-extrabold text-ink-950">!</span>
                <span>{error}</span>
              </p>
            )}
          </div>
        </section>

        <div data-testid="register-actions" className="rise flex w-full items-stretch gap-4 [animation-delay:120ms] max-sm:flex-col-reverse max-sm:gap-3">
          <button type="button" onClick={goHome} disabled={starting} className={`${SECONDARY_CTA} flex-1`} data-testid="register-back">
            <ArrowLeftIcon className="size-[1.1em] shrink-0" />
            Geri qayıt
          </button>
          <button
            ref={submitRef}
            type="submit"
            disabled={starting || !signedInAs}
            aria-busy={starting}
            className={`${PRIMARY_CTA} flex-[2] disabled:opacity-55`}
            data-testid="register-submit"
          >
            {starting ? 'Quiz hazırlanır…' : 'Yarışa başla'}
            {!starting && <PlayIcon className="size-[0.85em] shrink-0" />}
          </button>
        </div>
      </form>
    </GameShowShell>
  )
}
