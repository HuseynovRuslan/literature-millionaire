import { isAxiosError } from 'axios'
import { api } from './client'

/**
 * "Email ilə daxil ol": for players who are not in QRLog. A six-digit code is mailed to the address; typing it
 * back here returns the same kind of sign-in ticket a QRLog sign-in does, and the quiz is started with that.
 * See the backend's IEmailLoginService for the whole exchange.
 */
export interface EmailLoginStarted {
  /** Stays in this browser and goes back with the code: a code alone, read over somebody's shoulder, is nothing. */
  loginId: string
  /** The address as the server normalised it - what the screen says the code went to. */
  email: string
  secondsToLive: number
  resendAfterSeconds: number
}

/** The code matched. `signInTicket` is who plays; the name and address are only for the welcome screen. */
export interface EmailLoginVerified {
  fullName: string
  email: string
  signInTicket: string
}

/** Why a code was not sent or not accepted: the server's sentence, plus what the screen needs to act on it. */
export class EmailLoginError extends Error {
  readonly code: string | null
  readonly retryAfterSeconds: number | null

  constructor(message: string, code: string | null, retryAfterSeconds: number | null = null) {
    super(message)
    this.name = 'EmailLoginError'
    this.code = code
    this.retryAfterSeconds = retryAfterSeconds
  }
}

function toEmailLoginError(err: unknown): EmailLoginError {
  if (isAxiosError(err) && err.response) {
    const data = err.response.data as { code?: string; detail?: string; retryAfterSeconds?: number } | undefined
    if (data?.detail) return new EmailLoginError(data.detail, data.code ?? null, data.retryAfterSeconds ?? null)
    if (err.response.status === 429) {
      return new EmailLoginError('Çox tez-tez cəhd edilir. Bir az gözləyib yenidən yoxlayın.', 'RATE_LIMITED')
    }
  }
  return new EmailLoginError('Serverlə əlaqə alınmadı. Yenidən cəhd edin.', null)
}

/** Whether a mailbox is configured. False (or no answer) means the screen offers QRLog only. */
export async function getEmailLoginEnabled(signal?: AbortSignal): Promise<boolean> {
  try {
    const { data } = await api.get<{ enabled: boolean }>('/api/email-login', { signal })
    return data.enabled === true
  } catch {
    return false
  }
}

export async function startEmailLogin(fullName: string, email: string): Promise<EmailLoginStarted> {
  try {
    const { data } = await api.post<EmailLoginStarted>('/api/email-login/start', { fullName, email })
    return data
  } catch (err) {
    throw toEmailLoginError(err)
  }
}

export async function verifyEmailLogin(loginId: string, code: string): Promise<EmailLoginVerified> {
  try {
    const { data } = await api.post<EmailLoginVerified>('/api/email-login/verify', { loginId, code })
    return data
  } catch (err) {
    throw toEmailLoginError(err)
  }
}
