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
}

export type QrLoginStatus =
  | { status: 'pending' }
  | { status: 'confirmed'; fullName: string; phoneNumber: string }

export async function startQrLogin(signal?: AbortSignal): Promise<QrLoginStarted> {
  const { data } = await api.post<QrLoginStarted>('/api/qrlog-login/start', null, { signal })
  return data
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
    const { data } = await api.get<{ status: string; fullName: string | null; phoneNumber: string | null }>(
      `/api/qrlog-login/${encodeURIComponent(code)}`,
      { params: { secret: pollSecret }, signal },
    )
    if (data.status === 'confirmed' && data.fullName && data.phoneNumber) {
      return { status: 'confirmed', fullName: data.fullName, phoneNumber: data.phoneNumber }
    }
    return { status: 'pending' }
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 404) return 'expired'
    throw err
  }
}
