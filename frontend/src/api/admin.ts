import { isAxiosError } from 'axios'
import { api } from './client'

/**
 * The admin panel's API. Everything here rides on the admin session cookie, which the browser sends by itself
 * (same origin) and no script can read (HttpOnly).
 *
 * Every state-changing call carries X-Kitabxana-Admin: the server refuses a POST, PUT or DELETE under
 * /api/admin without it, which is what stops another website from acting through an admin's open session.
 */
const ADMIN_HEADERS = { 'X-Kitabxana-Admin': '1' }

export interface AdminSession {
  fullName: string
  /** Masked by the server, e.g. "+994 50 *** ** 34". */
  phone: string
}

export interface AuditEntry {
  id: number
  atUtc: string
  actorName: string
  actorPhone: string
  action: string
  entityType: string | null
  entityId: string | null
  details: string | null
}

/** Why a sign-in or a request did not get through, in the terms the screen acts on. */
export type AdminFailure = 'signed-out' | 'not-an-admin' | 'expired' | 'network'

export function adminFailure(err: unknown): AdminFailure {
  if (!isAxiosError(err) || !err.response) return 'network'
  const code = (err.response.data as { code?: string } | undefined)?.code
  if (err.response.status === 403) return 'not-an-admin'
  if (code === 'SIGN_IN_EXPIRED' || code === 'LINK_INVALID') return 'expired'
  return 'signed-out'
}

/** The current session, or null when nobody is signed in (or the session no longer qualifies). */
export async function getAdminSession(signal?: AbortSignal): Promise<AdminSession | null> {
  try {
    const { data } = await api.get<AdminSession>('/api/admin/session', { signal })
    return data
  } catch (err) {
    if (isAxiosError(err) && (err.response?.status === 401 || err.response?.status === 403)) return null
    throw err
  }
}

export async function signInWithTicket(signInTicket: string): Promise<AdminSession> {
  const { data } = await api.post<AdminSession>('/api/admin/session', { signInTicket }, { headers: ADMIN_HEADERS })
  return data
}

export async function signInWithLink(token: string): Promise<AdminSession> {
  const { data } = await api.post<AdminSession>('/api/admin/session/link', { token }, { headers: ADMIN_HEADERS })
  return data
}

export async function signOut(): Promise<void> {
  await api.delete('/api/admin/session', { headers: ADMIN_HEADERS })
}

export async function getAuditLog(signal?: AbortSignal): Promise<AuditEntry[]> {
  const { data } = await api.get<AuditEntry[]>('/api/admin/audit', { params: { take: 200 }, signal })
  return data
}
