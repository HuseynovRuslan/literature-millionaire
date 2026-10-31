import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { endQrLogin } from '../api/qrLogin'

/**
 * Who has signed in, by whichever door: QRLog vouched for a phone, or an emailed code proved an address. Either
 * way the ticket is what the quiz is started with; the rest is only shown.
 */
export type SignedIn =
  | { method: 'qrlog'; fullName: string; phoneNumber: string; signInTicket: string }
  | { method: 'email'; fullName: string; email: string; signInTicket: string }

/**
 * How long a sign-in is offered as still good. The server keeps a ticket for ten minutes
 * (QrLoginService.TicketLifetime) and has the last word; letting go a little earlier here means a player who
 * wandered off comes back to the sign-in, not to a start button that is about to be refused.
 */
const HOLD_MS = 9 * 60_000

interface SignInContextValue {
  /** Who is signed in on this screen, or null - then the sign-in is the screen. */
  signedIn: SignedIn | null
  signIn: (identity: SignedIn) => void
  /** Lets go of the sign-in on purpose: "Bu mən deyiləm", or the quiz has started and spent the ticket. */
  signOut: () => Promise<void>
  /**
   * A sign-in has been let go of in this tab, so the next one must be a genuinely new code: resuming the one just
   * walked away from (or just spent on a quiz) would hand the screen straight back to the last person.
   */
  startFresh: boolean
  /** Settles once a sign-out in flight has reached the server; a new QR asked for before then could be ended by it. */
  afterSignOut: () => Promise<void>
  /** What the email form held last, so it comes back filled in after a sign-out or an expired ticket. */
  lastEmail: { fullName: string; email: string }
}

const SignInContext = createContext<SignInContextValue | null>(null)

/**
 * The sign-in, held above the screens rather than inside one: the player signs in on the front door, and the
 * categories, the "no quiz right now" notice and the registration screen all know who it is. Memory only - a
 * ticket is a bearer pass, and nothing of it is written to storage.
 */
export function SignInProvider({ children }: { children: ReactNode }) {
  const [signedIn, setSignedIn] = useState<SignedIn | null>(null)
  const [startFresh, setStartFresh] = useState(false)
  const [lastEmail, setLastEmail] = useState({ fullName: '', email: '' })
  const ending = useRef<Promise<void>>(Promise.resolve())

  const signIn = useCallback((identity: SignedIn) => {
    if (identity.method === 'email') setLastEmail({ fullName: identity.fullName, email: identity.email })
    setSignedIn(identity)
  }, [])

  const signOut = useCallback(async () => {
    setSignedIn(null)
    setStartFresh(true)
    const ended = endQrLogin()
    ending.current = ended
    await ended
  }, [])

  const afterSignOut = useCallback(() => ending.current, [])

  useEffect(() => {
    if (!signedIn) return
    const id = window.setTimeout(() => void signOut(), HOLD_MS)
    return () => window.clearTimeout(id)
  }, [signedIn, signOut])

  const value = useMemo(
    () => ({ signedIn, signIn, signOut, startFresh, afterSignOut, lastEmail }),
    [signedIn, signIn, signOut, startFresh, afterSignOut, lastEmail],
  )
  return <SignInContext.Provider value={value}>{children}</SignInContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useSignIn(): SignInContextValue {
  const ctx = useContext(SignInContext)
  if (!ctx) throw new Error('useSignIn must be used inside <SignInProvider>')
  return ctx
}
