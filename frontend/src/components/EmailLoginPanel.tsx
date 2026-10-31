import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { EmailLoginError, startEmailLogin, verifyEmailLogin, type EmailLoginVerified } from '../api/emailLogin'
import { MailIcon } from './home/GameShowArt'

const FIELD = 'field w-full rounded-2xl px-4 py-3 text-[clamp(1rem,1.2vw,1.15rem)] font-semibold'
const LABEL = 'mb-1.5 block text-left text-[0.72rem] font-bold uppercase tracking-[0.12em] text-fg-3'
const ACTION = 'btn btn-primary min-h-12 w-full px-5 text-[clamp(1rem,1.15vw,1.1rem)]'
const QUIET = 'tap rounded-xl px-3 py-2 text-[clamp(0.88rem,1vw,0.95rem)] font-bold text-fg-2 underline decoration-white/30 underline-offset-4 hover:text-fg disabled:no-underline disabled:opacity-60'

type Step =
  | { kind: 'details' }
  | { kind: 'code'; loginId: string; email: string }

/**
 * Seconds left before another code may be asked for, counted down once a second. Only a hint: the server is what
 * enforces the wait, and says how long is left if it is asked too early.
 */
function useCountdown(): [number, (seconds: number) => void] {
  const [left, setLeft] = useState(0)
  useEffect(() => {
    if (left <= 0) return
    const id = window.setTimeout(() => setLeft((s) => s - 1), 1000)
    return () => window.clearTimeout(id)
  }, [left])
  return [left, setLeft]
}

/**
 * "Email ilə daxil ol": name and address, then the six-digit code mailed to that address. Sits inside the
 * registration form, so it has no form of its own (forms do not nest): Enter in a field presses this panel's
 * own button, never the quiz's start button.
 */
export default function EmailLoginPanel({ onVerified, initialName = '', initialEmail = '' }: {
  onVerified: (identity: EmailLoginVerified) => void
  /** What the player typed last time, when the panel comes back after a sign-out or an expired ticket. */
  initialName?: string
  initialEmail?: string
}) {
  const [step, setStep] = useState<Step>({ kind: 'details' })
  const [fullName, setFullName] = useState(initialName)
  const [email, setEmail] = useState(initialEmail)
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const codeRef = useRef<HTMLInputElement>(null)
  const [resendIn, setResendIn] = useCountdown()

  useEffect(() => {
    if (step.kind === 'code') codeRef.current?.focus()
  }, [step])

  async function send() {
    if (busy) return
    if (fullName.trim().length < 2) { setError('Ad və soyadı yazın.'); return }
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) { setError('Email ünvanı düzgün deyil.'); return }
    setBusy(true)
    setError(null)
    try {
      const started = await startEmailLogin(fullName.trim(), email.trim())
      setCode('')
      setStep({ kind: 'code', loginId: started.loginId, email: started.email })
      setResendIn(started.resendAfterSeconds)
    } catch (err) {
      setError(err instanceof EmailLoginError ? err.message : 'Kodu göndərmək alınmadı.')
      if (err instanceof EmailLoginError && err.retryAfterSeconds) setResendIn(err.retryAfterSeconds)
    } finally {
      setBusy(false)
    }
  }

  async function verify() {
    if (busy || step.kind !== 'code') return
    const digits = code.replace(/\D/g, '')
    if (digits.length !== 6) { setError('Məktubdakı 6 rəqəmli kodu yazın.'); return }
    setBusy(true)
    setError(null)
    try {
      onVerified(await verifyEmailLogin(step.loginId, digits))
    } catch (err) {
      setError(err instanceof EmailLoginError ? err.message : 'Kodu yoxlamaq alınmadı.')
      setCode('')
      codeRef.current?.focus()
    } finally {
      setBusy(false)
    }
  }

  /** Enter presses this panel's button: the outer form's own submit would try to start the quiz. */
  function onEnter(e: KeyboardEvent<HTMLInputElement>, action: () => Promise<void>) {
    if (e.key !== 'Enter') return
    e.preventDefault()
    void action()
  }

  return (
    <div className="flex min-w-0 flex-col items-center text-center" data-testid="email-login-panel" aria-live="polite">
      <span className="grid size-16 place-items-center rounded-full bg-brand/20 text-brand-soft ring-1 ring-brand/40 max-sm:size-14">
        <MailIcon className="size-8 max-sm:size-7" />
      </span>

      {step.kind === 'details' ? (
        <>
          <p lang="az" className="mt-4 font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-extrabold">Email ilə daxil olun</p>
          <p lang="az" className="mt-1 max-w-[36ch] text-[clamp(0.9rem,1.05vw,1rem)] font-medium leading-snug text-fg-2">
            QRLog tətbiqiniz yoxdursa: adınızı və email ünvanınızı yazın, ünvana təsdiq kodu göndərəcəyik.
          </p>
          <div className="mt-5 flex w-full max-w-[24rem] flex-col gap-3">
            <label className="block">
              <span className={LABEL}>Ad və soyad</span>
              <input
                className={FIELD}
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                onKeyDown={(e) => onEnter(e, send)}
                autoComplete="name"
                maxLength={120}
                placeholder="Məsələn: Leyla Əliyeva"
                data-testid="email-login-name"
              />
            </label>
            <label className="block">
              <span className={LABEL}>Email</span>
              <input
                className={FIELD}
                type="email"
                inputMode="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                onKeyDown={(e) => onEnter(e, send)}
                autoComplete="email"
                autoCapitalize="off"
                spellCheck={false}
                maxLength={254}
                placeholder="ad@mail.az"
                data-testid="email-login-email"
              />
            </label>
            <button type="button" onClick={() => void send()} disabled={busy} aria-busy={busy} className={`${ACTION} mt-1`} data-testid="email-login-send">
              {busy ? 'Göndərilir…' : 'Kod göndər'}
            </button>
          </div>
        </>
      ) : (
        <>
          <p lang="az" className="mt-4 font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-extrabold">Kodu daxil edin</p>
          <p lang="az" className="mt-1 max-w-[36ch] text-[clamp(0.9rem,1.05vw,1rem)] font-medium leading-snug text-fg-2">
            6 rəqəmli kodu <span className="font-bold text-fg [overflow-wrap:anywhere]" data-testid="email-login-sent-to">{step.email}</span> ünvanına göndərdik.
            Məktub görünmürsə, «Spam» qovluğuna da baxın.
          </p>
          <div className="mt-5 flex w-full max-w-[24rem] flex-col gap-3">
            <label className="block">
              <span className={LABEL}>Təsdiq kodu</span>
              <input
                ref={codeRef}
                className={`${FIELD} text-center font-display text-[clamp(1.5rem,2vw,1.9rem)] tracking-[0.4em] tabular-nums`}
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                onKeyDown={(e) => onEnter(e, verify)}
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                placeholder="••••••"
                aria-invalid={error ? true : undefined}
                data-testid="email-login-code"
              />
            </label>
            <button type="button" onClick={() => void verify()} disabled={busy || code.length !== 6} aria-busy={busy} className={`${ACTION} mt-1`} data-testid="email-login-verify">
              {busy ? 'Yoxlanılır…' : 'Təsdiqlə'}
            </button>
            <div className="flex flex-wrap items-center justify-center gap-x-2">
              <button type="button" onClick={() => void send()} disabled={busy || resendIn > 0} className={QUIET} data-testid="email-login-resend">
                {resendIn > 0 ? `Kodu yenidən göndər (${resendIn} san.)` : 'Kodu yenidən göndər'}
              </button>
              <button type="button" onClick={() => { setStep({ kind: 'details' }); setError(null) }} disabled={busy} className={QUIET} data-testid="email-login-change">
                Email-i dəyiş
              </button>
            </div>
          </div>
        </>
      )}

      {error && (
        <p role="alert" className="mt-3 max-w-[24rem] text-[clamp(0.9rem,1vw,0.98rem)] font-bold leading-snug text-bad" data-testid="email-login-error">
          {error}
        </p>
      )}
    </div>
  )
}
