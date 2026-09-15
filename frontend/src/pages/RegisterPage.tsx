import { useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import CarpetFrame from '../components/national/CarpetFrame'
import { KioskHeader } from '../components/national/KioskBrand'
import { Buta, ButaRule } from '../components/national/Ornaments'
import { useGame } from '../game/GameContext'

/** Mirrors the backend rule: Azerbaijani mobile in 0XX…, 994XX… or +994XX… form (spaces and dashes allowed). */
const PHONE = /^(?:\+?994|0)(?:10|50|51|55|60|70|77|99)\d{7}$/
const compact = (v: string) => v.replace(/[\s\-()]/g, '')

const FIELD =
  'w-full rounded-xl border-2 border-[var(--p-gold-light)] bg-white px-6 py-4 text-[clamp(1.4rem,2.2vw,2.2rem)] text-[var(--p-ink)] shadow-[var(--p-shadow)] outline-none focus:border-[var(--p-burgundy)] focus:ring-4 focus:ring-[rgba(125,22,29,0.18)]'

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
  const [fieldError, setFieldError] = useState<string | null>(null)
  // One start per registration. The context only ignores taps while a request is in flight; once it
  // succeeds, the form is briefly interactive again before navigation. With one attempt per campaign
  // a second tap in that window would get a 409 and wipe the quiz that was just started.
  const submittedRef = useRef(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (starting || submittedRef.current) return
    const name = fullName.trim().replace(/\s+/g, ' ')
    if (name.length < 2) return setFieldError('Ad və soyadınızı yazın (ən azı 2 hərf).')
    if (!PHONE.test(compact(phone))) return setFieldError('Telefon nömrəsi düzgün deyil. Nümunə: 050 123 45 67')
    setFieldError(null)
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
      <main className="kiosk paper flex flex-col">
        <CarpetFrame />
        <KioskHeader />
        <div role="alert" className="relative z-0 flex min-h-0 flex-1 flex-col items-center justify-center text-center" style={{ padding: '0.4rem calc(var(--frame) + 2rem) calc(var(--frame) + 1.5rem)' }} data-testid="attempt-limit">
          <p className="font-display text-[clamp(1.4rem,2.2vw,2.2rem)] font-semibold tracking-[0.18em] text-[var(--p-burgundy)]">AYIN KİTABI</p>
          <h1 className="mt-4 max-w-[24ch] font-display text-[clamp(2.4rem,4.6vw,4.8rem)] font-bold leading-tight text-[var(--p-ink)]">
            Bu kampaniyada artıq iştirak etmisiniz.
          </h1>
          <p className="mt-4 max-w-[44ch] font-display text-[clamp(1.4rem,2.2vw,2.3rem)] font-semibold leading-snug text-[var(--p-indigo)]">
            Hər telefon nömrəsi ilə yalnız bir dəfə iştirak etmək mümkündür.
          </p>
          <ButaRule className="my-7 w-full max-w-[30rem]" />
          <p className="max-w-[44ch] text-[clamp(1.1rem,1.6vw,1.7rem)] leading-relaxed text-[var(--p-ink-2)]">
            Növbəti Ayın kitabı kampaniyasında sizi yenidən gözləyirik.
          </p>
          <button type="button" onClick={goHome} className="tap paper-cta mt-10 flex min-h-[clamp(5rem,11vh,7rem)] w-full max-w-[30rem] items-center justify-center rounded-full px-10 font-display text-[clamp(1.8rem,3vw,3rem)] font-bold tracking-[0.05em]">
            Ana səhifə
          </button>
        </div>
      </main>
    )
  }

  return (
    <main className="kiosk paper flex flex-col">
      <CarpetFrame />
      <KioskHeader />
      <div className="relative z-0 flex min-h-0 flex-1 flex-col items-center justify-center" style={{ padding: '0.4rem calc(var(--frame) + 2rem) calc(var(--frame) + 1.2rem)' }}>
        <form onSubmit={submit} noValidate className="relative w-full max-w-[56rem]">
          <Buta className="absolute -left-3 -top-4 z-10 h-12 w-9" flip />
          <Buta className="absolute -right-3 -top-4 z-10 h-12 w-9" />
          <div className="flex flex-col items-center rounded-xl border-[3px] border-[var(--p-gold)] bg-white px-[clamp(1.5rem,4vw,4rem)] py-[clamp(1.2rem,2.6vh,2.4rem)] text-center shadow-[var(--p-shadow)] outline outline-1 outline-offset-[-9px] outline-[var(--p-gold-light)]">
            <p className="font-display text-[clamp(1.3rem,2vw,2rem)] font-semibold tracking-[0.18em] text-[var(--p-burgundy)]">AYIN KİTABI</p>
            <h1 className="mt-1 font-display text-[clamp(2.2rem,4.6vw,4.6rem)] font-bold leading-none text-[var(--p-ink)]">Qeydiyyat</h1>
            <p className="mt-2 text-[clamp(1rem,1.4vw,1.4rem)] text-[var(--p-ink-2)]">Quizə başlamaq üçün ad-soyad və telefon nömrənizi yazın.</p>
            <ButaRule className="my-5 w-full max-w-[28rem]" />

            <label className="flex w-full flex-col items-start gap-2 text-left">
              <span className="text-[clamp(1rem,1.4vw,1.4rem)] font-medium text-[var(--p-ink-2)]">Ad və soyad</span>
              <input value={fullName} onChange={(e) => setFullName(e.target.value)} className={FIELD} maxLength={120} autoComplete="off" autoCapitalize="words" spellCheck={false} placeholder="Məsələn: Ayşə Məmmədova" data-testid="fullName" />
            </label>
            <label className="mt-4 flex w-full flex-col items-start gap-2 text-left">
              <span className="text-[clamp(1rem,1.4vw,1.4rem)] font-medium text-[var(--p-ink-2)]">Telefon nömrəsi</span>
              <input value={phone} onChange={(e) => setPhone(e.target.value)} className={`${FIELD} tabular-nums`} inputMode="tel" autoComplete="off" maxLength={20} placeholder="050 123 45 67" data-testid="phone" />
            </label>

            {(fieldError || error) && (
              <p role="alert" className="mt-4 w-full rounded-2xl border-2 border-[#b32a31] bg-white px-5 py-3 text-[clamp(1rem,1.3vw,1.3rem)] text-[var(--p-ink)]">{fieldError ?? error}</p>
            )}

            <div className="mt-6 flex w-full flex-col gap-4 sm:flex-row">
              <button type="submit" disabled={starting} aria-busy={starting} className="tap paper-cta flex min-h-[6rem] flex-1 items-center justify-center rounded-full font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-bold tracking-[0.05em]">
                {starting ? 'Quiz hazırlanır…' : 'QUİZƏ BAŞLA'}
              </button>
              <button type="button" onClick={goHome} disabled={starting} className="tap paper-ghost flex min-h-[6rem] flex-1 items-center justify-center rounded-full font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-semibold disabled:opacity-60">
                Ana səhifə
              </button>
            </div>
          </div>
        </form>
      </div>
    </main>
  )
}
