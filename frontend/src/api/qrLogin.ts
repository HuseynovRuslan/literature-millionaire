import { isAxiosError } from 'axios'
import { api } from './client'

/**
 * "QRLog ilə davam et": an employee already registered in QRLog signs in by scanning a QR with the
 * QRLog app instead of typing their name and phone.
 *
 * Only the code goes into the QR. The poll secret stays in this browser, so someone who photographed
 * the QR from across the room cannot read the identity that lands on it - see the backend's
 * IQrLoginService for the whole exchange.
 */
export interface QrLoginStarted {
  code: string
  pollSecret: string
  expiresAtUtc: string
  secondsToLive: number
  /**
   * Where QRLog approves this code without a QR, for someone who is already on their phone: the phone cannot
   * scan its own screen. Null when the server has no QRLog app address configured.
   */
  appConfirmUrl?: string | null
}

/**
 * `signInTicket` is what the quiz is started with. The name and phone are only for the welcome screen: the
 * server takes who is playing from the ticket and ignores anything the browser says about itself.
 */
export type QrLoginStatus =
  | { status: 'pending' }
  | { status: 'confirmed'; fullName: string; phoneNumber: string; signInTicket: string }

/**
 * The sign-in this browser last started, if it is still alive - known to the server by a cookie every window
 * of the browser shares. Null when there is none. This is how a window with none of the original page's
 * storage (an installed app, a fresh tab QRLog handed us back to) carries on waiting instead of starting over.
 */
export async function resumeQrLogin(signal?: AbortSignal): Promise<QrLoginStarted | null> {
  const { status, data } = await api.get<QrLoginStarted | ''>('/api/qrlog-login/resume', { signal })
  return status === 200 && data && typeof data === 'object' ? data : null
}

export async function startQrLogin(signal?: AbortSignal): Promise<QrLoginStarted> {
  const { data } = await api.post<QrLoginStarted>('/api/qrlog-login/start', null, { signal })
  return data
}

/**
 * Ends the sign-in this browser is carrying: signing out, handing the phone to the next person, or asking
 * for a genuinely new QR. The server forgets the pending login and the ticket it issued, and clears the
 * cookie that pointed at it - without that, every later screen resumes a sign-in that is already spent.
 *
 * Never throws. Nothing on screen can act on a failure here, and a sign-out that fails because the network
 * blinked must still let go of the person standing in front of it; the sign-in expires by itself in minutes.
 */
export async function endQrLogin(signal?: AbortSignal): Promise<void> {
  try {
    await api.delete('/api/qrlog-login/pending', { signal })
  } catch {
    // Deliberately ignored - see above.
  }
}

/**
 * Where the sign-in stands. `expired` covers every ending the kiosk treats the same way - the code ran
 * out, it was already used, or the server no longer knows it - because the player can only do one
 * thing about any of them: ask for a new QR.
 */
export async function pollQrLogin(
  code: string,
  pollSecret: string,
  signal?: AbortSignal,
): Promise<QrLoginStatus | 'expired'> {
  try {
    const { data } = await api.get<{ status: string; fullName: string | null; phoneNumber: string | null; signInTicket?: string | null }>(
      `/api/qrlog-login/${encodeURIComponent(code)}`,
      { params: { secret: pollSecret }, signal },
    )
    if (data.status === 'confirmed' && data.fullName && data.phoneNumber && data.signInTicket) {
      return { status: 'confirmed', fullName: data.fullName, phoneNumber: data.phoneNumber, signInTicket: data.signInTicket }
    }
    return { status: 'pending' }
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 404) return 'expired'
    throw err
  }
}
