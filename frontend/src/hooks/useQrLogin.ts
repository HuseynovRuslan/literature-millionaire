import { useCallback, useEffect, useRef, useState } from 'react'
import { pollQrLogin, resumeQrLogin, startQrLogin, type QrLoginStarted } from '../api/qrLogin'

/**
 * What the QR actually carries. An absolute URL on this origin: the QRLog app recognises the /qr/
 * path, and an employee who scans it with their ordinary camera lands on a page that explains what to
 * do rather than on a meaningless string.
 */
export const qrValueFor = (code: string) => `${window.location.origin}/qr/${code}`

/** How often the kiosk asks whether QRLog has confirmed the code yet. */
const POLL_MS = 1500
/** A momentary network blip must not cancel a sign-in the employee is in the middle of. */
const TOLERATED_FAILURES = 4

/**
 * The sign-in in progress, kept for this tab only.
 *
 * On a phone the employee leaves this page for QRLog and comes back, and a browser is free to reload the page
 * it was left on. Without this, coming back would mint a fresh code - and the one QRLog has just approved would
 * be thrown away, which reads as "nothing happened". Session storage is per tab and goes when the tab does; it
 * holds a code that expires in minutes and is spent on first use.
 */
const PENDING_KEY = 'qrlog-pending'

function remember(started: QrLoginStarted) {
  try {
    window.sessionStorage.setItem(PENDING_KEY, JSON.stringify(started))
  } catch {
    // Private browsing, or storage turned off: the sign-in still works, it just cannot survive a reload.
  }
}

function forget() {
  try {
    window.sessionStorage.removeItem(PENDING_KEY)
  } catch {
    // Nothing to clean up if it could never be written in the first place.
  }
}

/** The sign-in this tab left behind, if it is still worth resuming (more than a few seconds left). */
function recall(): QrLoginStarted | null {
  try {
    const stored = window.sessionStorage.getItem(PENDING_KEY)
    if (!stored) return null
    const started = JSON.parse(stored) as QrLoginStarted
    if (!started?.code || !started.pollSecret) return null
    return Date.parse(started.expiresAtUtc) - Date.now() > 5_000 ? started : null
  } catch {
    return null
  }
}

export type QrLoginState =
  | { kind: 'idle' }
  | { kind: 'starting' }
  | { kind: 'waiting'; code: string; qrValue: string; secondsLeft: number; appConfirmUrl: string | null }
  | { kind: 'confirmed'; fullName: string; phoneNumber: string }
  | { kind: 'expired' }
  | { kind: 'error' }

/** Who QRLog vouched for, and the ticket the quiz must be started with. */
export interface QrLoginIdentity {
  fullName: string
  phoneNumber: string
  signInTicket: string
}

/**
 * Drives "QRLog ilə davam et": opens a sign-in, counts the code down, polls until QRLog confirms it, and stops
 * on its own when the code expires. Everything is cancelled when the screen is left, so a forgotten QR is not
 * still being polled in the background.
 *
 * A sign-in survives leaving this page and coming back - the phone route, where QRLog itself is opened to
 * approve the code - so an approval given while this screen was in the background is picked up, not lost.
 */
export function useQrLogin(onConfirmed: (identity: QrLoginIdentity) => void) {
  const [state, setState] = useState<QrLoginState>({ kind: 'idle' })
  const timers = useRef<number[]>([])
  const aborter = useRef<AbortController | null>(null)
  // Held in a ref, and refreshed after each render, so a re-render of the caller never restarts the
  // poll mid sign-in and the callback is never a stale one.
  const confirmed = useRef(onConfirmed)
  useEffect(() => {
    confirmed.current = onConfirmed
  })

  const visibility = useRef<(() => void) | null>(null)

  const stop = useCallback(() => {
    timers.current.forEach((id) => window.clearInterval(id))
    timers.current = []
    aborter.current?.abort()
    aborter.current = null
    if (visibility.current) {
      document.removeEventListener('visibilitychange', visibility.current)
      visibility.current = null
    }
  }, [])

  const cancel = useCallback(() => {
    stop()
    forget()
    setState({ kind: 'idle' })
  }, [stop])

  useEffect(() => stop, [stop])

  const begin = useCallback(async () => {
    stop()
    const controller = new AbortController()
    aborter.current = controller

    // A sign-in already under way is resumed, never replaced: first the one this tab remembers, then the one
    // this browser last started (a cookie, so a different window - an installed app, a fresh tab QRLog handed us
    // back to - finds it too). Only when there is none does a new code get minted.
    let started = recall()
    if (!started) {
      setState({ kind: 'starting' })
      try {
        started = (await resumeQrLogin(controller.signal).catch(() => null)) ?? (await startQrLogin(controller.signal))
      } catch {
        if (!controller.signal.aborted) setState({ kind: 'error' })
        return
      }
      if (controller.signal.aborted) return
      remember(started)
    }

    const deadline = Date.parse(started.expiresAtUtc)
    // Fall back to the server's own figure if its timestamp is unparseable for any reason.
    const secondsLeft = () =>
      Number.isFinite(deadline) ? Math.max(0, Math.ceil((deadline - Date.now()) / 1000)) : started.secondsToLive
    setState({
      kind: 'waiting',
      code: started.code,
      qrValue: qrValueFor(started.code),
      secondsLeft: secondsLeft(),
      appConfirmUrl: started.appConfirmUrl ?? null,
    })

    const tick = window.setInterval(() => {
      const left = secondsLeft()
      if (left <= 0) {
        stop()
        forget()
        setState({ kind: 'expired' })
        return
      }
      setState((s) => (s.kind === 'waiting' ? { ...s, secondsLeft: left } : s))
    }, 1000)

    let failures = 0
    const pending = started
    const askOnce = async () => {
      try {
        const result = await pollQrLogin(pending.code, pending.pollSecret, controller.signal)
        failures = 0
        if (result === 'expired') {
          stop()
          forget()
          setState({ kind: 'expired' })
          return
        }
        if (result.status === 'confirmed') {
          stop()
          forget()
          setState({ kind: 'confirmed', fullName: result.fullName, phoneNumber: result.phoneNumber })
          confirmed.current({ fullName: result.fullName, phoneNumber: result.phoneNumber, signInTicket: result.signInTicket })
        }
      } catch {
        if (controller.signal.aborted) return
        failures += 1
        if (failures > TOLERATED_FAILURES) {
          stop()
          setState({ kind: 'error' })
        }
      }
    }

    const ask = window.setInterval(() => void askOnce(), POLL_MS)

    // Back from QRLog: ask at once instead of waiting for the next tick, because a phone browser slows or
    // stops timers in a tab nobody is looking at.
    const onVisible = () => {
      if (document.visibilityState === 'visible') void askOnce()
    }
    document.addEventListener('visibilitychange', onVisible)
    visibility.current = onVisible

    timers.current = [tick, ask]
    void askOnce()
  }, [stop])

  return { state, begin, cancel }
}
