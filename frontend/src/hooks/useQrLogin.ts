import { useCallback, useEffect, useRef, useState } from 'react'
import { pollQrLogin, startQrLogin, type QrLoginStarted } from '../api/qrLogin'

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

export type QrLoginState =
  | { kind: 'idle' }
  | { kind: 'starting' }
  | { kind: 'waiting'; code: string; qrValue: string; secondsLeft: number }
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
 * Drives "QRLog ilə davam et" from the kiosk side: opens a sign-in, counts the QR down, polls until
 * QRLog confirms it, and stops on its own when the code expires. Everything is cancelled when the
 * screen is left, so a forgotten QR is not still being polled in the background.
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

  const stop = useCallback(() => {
    timers.current.forEach((id) => window.clearInterval(id))
    timers.current = []
    aborter.current?.abort()
    aborter.current = null
  }, [])

  const cancel = useCallback(() => {
    stop()
    setState({ kind: 'idle' })
  }, [stop])

  useEffect(() => stop, [stop])

  const begin = useCallback(async () => {
    stop()
    setState({ kind: 'starting' })
    const controller = new AbortController()
    aborter.current = controller

    let started: QrLoginStarted
    try {
      started = await startQrLogin(controller.signal)
    } catch {
      if (!controller.signal.aborted) setState({ kind: 'error' })
      return
    }
    if (controller.signal.aborted) return

    const deadline = Date.parse(started.expiresAtUtc)
    // Fall back to the server's own figure if its timestamp is unparseable for any reason.
    const secondsLeft = () =>
      Number.isFinite(deadline) ? Math.max(0, Math.ceil((deadline - Date.now()) / 1000)) : started.secondsToLive
    setState({ kind: 'waiting', code: started.code, qrValue: qrValueFor(started.code), secondsLeft: secondsLeft() })

    const tick = window.setInterval(() => {
      const left = secondsLeft()
      if (left <= 0) {
        stop()
        setState({ kind: 'expired' })
        return
      }
      setState((s) => (s.kind === 'waiting' ? { ...s, secondsLeft: left } : s))
    }, 1000)

    let failures = 0
    const ask = window.setInterval(async () => {
      try {
        const result = await pollQrLogin(started.code, started.pollSecret, controller.signal)
        failures = 0
        if (result === 'expired') {
          stop()
          setState({ kind: 'expired' })
          return
        }
        if (result.status === 'confirmed') {
          stop()
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
    }, POLL_MS)

    timers.current = [tick, ask]
  }, [stop])

  return { state, begin, cancel }
}
