import { useCallback, useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { adminFailure, getAdminSession, signInWithTicket, signOut, type AdminSession } from '../../api/admin'
import QrLoginPanel from '../../components/QrLoginPanel'
import BrandMark from '../../components/national/BrandMark'
import { PRODUCT_NAME } from '../../components/national/KioskBrand'
import { useQrLogin } from '../../hooks/useQrLogin'
import type { AdminOutletContext } from './adminContext'

/**
 * The admin panel: its sign-in screen and its frame.
 *
 * Nothing under /admin renders until the server has said who is signed in. The check is the server's, every
 * time (GET /api/admin/session): this component holds no token and decides nothing about access - it only
 * chooses between the sign-in screen and the panel. Any request that later comes back 401 or 403 (the session
 * ended, or the person was taken off the admin list) sends it back to the sign-in screen via `onSignedOut`.
 */
type SessionState =
  | { kind: 'checking' }
  | { kind: 'signed-out'; notice?: string }
  | { kind: 'signed-in'; session: AdminSession }
  | { kind: 'unreachable' }

const NAV: { to: string; label: string; ready: boolean }[] = [
  { to: '/admin', label: 'Ümumi baxış', ready: true },
  { to: '/admin/campaigns', label: 'Kampaniyalar', ready: true },
  { to: '/admin/results', label: 'Nəticələr', ready: true },
  { to: '/admin/images', label: 'Şəkillər', ready: true },
  { to: '/admin/books', label: 'Sual bankları', ready: true },
  { to: '/admin/questions-editor', label: 'Suallar', ready: true },
  { to: '/admin/import', label: 'Toplu idxal', ready: true },
  { to: '/admin/categories', label: 'Kateqoriyalar', ready: true },
  { to: '/admin/audit', label: 'Jurnal', ready: true },
]

export default function AdminApp() {
  const [state, setState] = useState<SessionState>({ kind: 'checking' })
  // Bumped to ask the server again (the "try again" button after a network failure).
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getAdminSession(controller.signal)
      .then((session) => setState(session ? { kind: 'signed-in', session } : { kind: 'signed-out' }))
      .catch(() => { if (!controller.signal.aborted) setState({ kind: 'unreachable' }) })
    return () => controller.abort()
  }, [attempt])

  const onSignedOut = useCallback(() => setState({ kind: 'signed-out', notice: 'Sessiyanın vaxtı bitib. Yenidən daxil olun.' }), [])

  if (state.kind === 'checking') {
    return <AdminCenter><p className="font-semibold text-fg-2" role="status">Yoxlanılır…</p></AdminCenter>
  }

  if (state.kind === 'unreachable') {
    return (
      <AdminCenter>
        <p role="alert" className="font-semibold text-fg">Serverlə əlaqə yoxdur.</p>
        <button type="button" onClick={() => { setState({ kind: 'checking' }); setAttempt((a) => a + 1) }} className="btn btn-primary mt-4 min-h-12 px-6">
          Yenidən yoxla
        </button>
      </AdminCenter>
    )
  }

  if (state.kind === 'signed-out') {
    return <AdminSignIn notice={state.notice} onSignedIn={(session) => setState({ kind: 'signed-in', session })} />
  }

  return (
    <AdminFrame
      session={state.session}
      onSignOut={async () => {
        try {
          await signOut()
        } finally {
          setState({ kind: 'signed-out', notice: 'Çıxış etdiniz.' })
        }
      }}
    >
      <Outlet context={{ session: state.session, onSignedOut } satisfies AdminOutletContext} />
    </AdminFrame>
  )
}

function AdminCenter({ children }: { children: React.ReactNode }) {
  return (
    <main className="arena flex min-h-dvh flex-col items-center justify-center px-4 text-center">
      {children}
    </main>
  )
}

/**
 * The sign-in screen: the same QRLog QR players use. Scanning proves who you are; whether that person may
 * enter is the admin list's decision, made on the server.
 */
function AdminSignIn({ notice, onSignedIn }: { notice?: string; onSignedIn: (session: AdminSession) => void }) {
  const [message, setMessage] = useState<string | undefined>(notice)
  const [busy, setBusy] = useState(false)
  // Each refused sign-in asks for a fresh QR: the one just used is spent.
  const [round, setRound] = useState(0)

  const qrLogin = useQrLogin(async (identity) => {
    setBusy(true)
    try {
      onSignedIn(await signInWithTicket(identity.signInTicket))
    } catch (err) {
      const failure = adminFailure(err)
      // A spent ticket can mean the sign-in already worked: the same approval can reach two copies of this
      // screen (QRLog hands the browser back to one tab while another is still waiting), and whichever got
      // there first spent the ticket on a session - which is a cookie, so it belongs to this browser too.
      // Telling the second copy "your sign-in expired" while the panel is open next door is the worst of
      // both: it is wrong, and it sends somebody who is signed in back to scanning.
      if (failure === 'expired') {
        const existing = await getAdminSession().catch(() => null)
        if (existing) {
          onSignedIn(existing)
          return
        }
      }
      setMessage(
        failure === 'not-an-admin'
          ? 'Bu nömrə idarəetmə panelinə giriş siyahısında deyil.'
          : failure === 'expired'
            ? 'Girişin vaxtı bitdi. Yeni QR kodu oxudun.'
            : 'Daxil olmaq alınmadı. Yenidən cəhd edin.',
      )
      setRound((r) => r + 1)
    } finally {
      setBusy(false)
    }
  })

  // On `begin` itself, which is stable - not on the object useQrLogin returns, which is new on every render and
  // would restart the QR on every render, so it never settled long enough to be scanned.
  const beginQrLogin = qrLogin.begin
  useEffect(() => {
    void beginQrLogin()
  }, [beginQrLogin, round])

  return (
    <main className="arena flex min-h-dvh flex-col items-center justify-center gap-6 px-4 py-10">
      <div className="flex items-center gap-3">
        <BrandMark size="sm" />
        <div className="text-left">
          <p className="font-display text-xl font-extrabold uppercase leading-none tracking-tight">{PRODUCT_NAME}</p>
          <p className="mt-1 text-xs font-bold uppercase tracking-[0.18em] text-fg-3">İdarəetmə paneli</p>
        </div>
      </div>

      <section className="card w-full max-w-[28rem] rounded-[2rem] px-6 py-8" aria-labelledby="admin-sign-in-title">
        <h1 id="admin-sign-in-title" className="mb-5 text-center font-display text-2xl font-bold">Panelə giriş</h1>
        {busy ? (
          <p role="status" className="py-16 text-center font-semibold text-fg-2">Daxil olunur…</p>
        ) : (
          <QrLoginPanel
            state={qrLogin.state}
            onRetry={() => void qrLogin.begin()}
            hint="Admin siyahısında olan nömrənizlə QRLog tətbiqindən skan edin."
          />
        )}
        {message && (
          <p role="alert" data-testid="admin-sign-in-message" className="mt-5 rounded-2xl bg-white/[0.06] px-4 py-3 text-center text-sm font-semibold text-fg ring-1 ring-white/15">
            {message}
          </p>
        )}
      </section>
    </main>
  )
}

function AdminFrame({ session, onSignOut, children }: { session: AdminSession; onSignOut: () => void; children: React.ReactNode }) {
  return (
    <div className="arena flex min-h-dvh flex-col">
      <header className="border-b border-white/10 bg-ink-950/60 backdrop-blur">
        <div className="mx-auto flex w-full max-w-[90rem] items-center gap-4 px-5 py-3 max-sm:px-3">
          <BrandMark size="sm" />
          <div className="min-w-0">
            <p className="font-display text-lg font-extrabold uppercase leading-none tracking-tight max-sm:text-base">{PRODUCT_NAME}</p>
            <p className="mt-0.5 text-[0.7rem] font-bold uppercase tracking-[0.18em] text-fg-3">İdarəetmə paneli</p>
          </div>
          <div className="ml-auto flex items-center gap-4 max-sm:gap-2">
            <div className="text-right max-sm:hidden" data-testid="admin-identity">
              <p className="text-sm font-bold text-fg">{session.fullName}</p>
              <p className="text-xs font-medium tabular-nums text-fg-3">{session.phone}</p>
            </div>
            <button type="button" onClick={onSignOut} className="btn btn-secondary min-h-11 px-4 text-sm" data-testid="admin-sign-out">
              Çıxış
            </button>
          </div>
        </div>
      </header>

      <div className="mx-auto flex w-full max-w-[90rem] flex-1 gap-6 px-5 py-6 max-md:flex-col max-sm:px-3">
        <nav aria-label="Panel bölmələri" className="w-56 shrink-0 max-md:w-full">
          <ul className="flex flex-col gap-1 max-md:flex-row max-md:flex-wrap">
            {NAV.map((item) => (
              <li key={item.to}>
                {item.ready ? (
                  <NavLink
                    to={item.to}
                    end={item.to === '/admin'}
                    className={({ isActive }) =>
                      `block rounded-xl px-3.5 py-2.5 text-sm font-bold transition-colors ${isActive ? 'bg-brand text-white' : 'text-fg-2 hover:bg-white/[0.06] hover:text-fg'}`
                    }
                  >
                    {item.label}
                  </NavLink>
                ) : (
                  // Shown so it is clear what the panel will hold; not a link until it exists.
                  <span className="flex items-center justify-between gap-2 rounded-xl px-3.5 py-2.5 text-sm font-bold text-fg-3/70" aria-disabled="true">
                    {item.label}
                    <span className="rounded-full bg-white/[0.06] px-2 py-0.5 text-[0.65rem] font-bold uppercase tracking-wider">tezliklə</span>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </nav>

        <main className="min-w-0 flex-1">{children}</main>
      </div>
    </div>
  )
}
