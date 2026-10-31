import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { signInWithLink } from '../../api/admin'

/**
 * Where a break-glass link lands (/admin/link#token), for signing in when QRLog is down.
 *
 * The token is in the fragment, which a browser never sends to any server, so it never appears in an access
 * log. It is read once, removed from the address bar straight away, and spent on the server.
 */
export default function AdminLinkPage() {
  const navigate = useNavigate()
  // Read once, on the first render: the token is removed from the address bar straight after.
  const [token] = useState(() => window.location.hash.replace(/^#/, ''))
  const [failed, setFailed] = useState(!token)
  const spent = useRef(false)

  useEffect(() => {
    if (spent.current) return
    spent.current = true
    window.history.replaceState(null, '', window.location.pathname)
    if (!token) return
    signInWithLink(token)
      .then(() => navigate('/admin', { replace: true }))
      .catch(() => setFailed(true))
  }, [navigate, token])

  return (
    <main className="arena flex min-h-dvh flex-col items-center justify-center px-4 text-center">
      {failed ? (
        <section className="card max-w-[28rem] rounded-3xl px-6 py-8" role="alert">
          <h1 className="font-display text-xl font-bold">Link işləmədi</h1>
          <p className="mt-2 text-fg-2">Bu link etibarsızdır, artıq istifadə olunub və ya vaxtı bitib. Yeni link lazımdır.</p>
          <Link to="/admin" className="btn btn-secondary mt-5 inline-flex min-h-11 px-5">Giriş səhifəsinə</Link>
        </section>
      ) : (
        <p role="status" className="font-semibold text-fg-2">Daxil olunur…</p>
      )}
    </main>
  )
}
