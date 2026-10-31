import { useEffect, useState } from 'react'
import { getEmailLoginEnabled } from '../api/emailLogin'
import { useSignIn, type SignedIn } from '../game/SignInContext'
import { useQrLogin } from '../hooks/useQrLogin'
import EmailLoginPanel from './EmailLoginPanel'
import QrLoginPanel from './QrLoginPanel'

/** How the player signs in. QRLog first: most players are colleagues already carrying it. */
type SignInMethod = 'qrlog' | 'email'

/**
 * The way in: QRLog's QR, or - once the server has a mailbox - a code mailed to the player. The QR opens as soon as
 * the panel is on screen, and whoever signs in is handed to SignInContext, which every screen after this one reads.
 */
export default function SignInPanel({ onSignedIn }: {
  /** Called after the context has the new sign-in, for whatever the screen around the panel does next. */
  onSignedIn?: (identity: SignedIn) => void
}) {
  const { signIn, startFresh, afterSignOut, lastEmail } = useSignIn()
  const [method, setMethod] = useState<SignInMethod>('qrlog')
  /** Offered only once the server says a mailbox is configured; until then (or without one) the panel is QRLog's. */
  const [emailEnabled, setEmailEnabled] = useState(false)

  function accept(identity: SignedIn) {
    signIn(identity)
    onSignedIn?.(identity)
  }

  const qrLogin = useQrLogin((identity) => accept({ method: 'qrlog', ...identity }))

  useEffect(() => {
    const controller = new AbortController()
    void getEmailLoginEnabled(controller.signal).then((enabled) => { if (!controller.signal.aborted) setEmailEnabled(enabled) })
    return () => controller.abort()
  }, [])

  // Open the sign-in as soon as the panel is up: the QR is the screen, not a step behind a button. A sign-out
  // still on its way to the server goes first, or it could end the code minted here.
  const beginQrLogin = qrLogin.begin
  useEffect(() => {
    let live = true
    void afterSignOut().then(() => { if (live) void beginQrLogin(startFresh ? { fresh: true } : undefined) })
    return () => { live = false }
  }, [afterSignOut, beginQrLogin, startFresh])

  return (
    <div className="flex min-w-0 flex-col gap-[clamp(1rem,2vh,1.4rem)]" data-testid="sign-in-panel">
      {emailEnabled && (
        <div role="group" aria-label="Giriş üsulu" className="mx-auto grid w-full max-w-[24rem] grid-cols-2 gap-1 rounded-2xl bg-white/[0.05] p-1 ring-1 ring-white/10" data-testid="sign-in-method">
          {([['qrlog', 'QRLog ilə'], ['email', 'Email ilə']] as const).map(([value, label]) => (
            <button
              key={value}
              type="button"
              aria-pressed={method === value}
              onClick={() => setMethod(value)}
              className={`tap min-h-11 rounded-xl px-3 font-display text-[clamp(0.95rem,1.05vw,1.05rem)] font-bold transition-colors ${
                method === value ? 'bg-brand text-white shadow-[0_0.4rem_1rem_-0.4rem_rgba(123,97,255,0.8)]' : 'text-fg-2 hover:text-fg'
              }`}
              data-testid={`sign-in-method-${value}`}
            >
              {label}
            </button>
          ))}
        </div>
      )}
      {emailEnabled && method === 'email' ? (
        <EmailLoginPanel
          initialName={lastEmail.fullName}
          initialEmail={lastEmail.email}
          onVerified={(verified) => {
            // The QR waiting in the other tab is not needed any more; a scan landing now would replace this
            // sign-in with somebody else's.
            qrLogin.cancel()
            accept({ method: 'email', ...verified })
          }}
        />
      ) : (
        <QrLoginPanel state={qrLogin.state} onRetry={() => void qrLogin.begin({ fresh: true })} />
      )}
    </div>
  )
}
