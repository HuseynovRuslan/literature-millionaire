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
export type AdminFailure = 'signed-out' | 'not-an-admin' | 'expired' | 'refused' | 'network'

export function adminFailure(err: unknown): AdminFailure {
  if (!isAxiosError(err) || !err.response) return 'network'
  const code = (err.response.data as { code?: string } | undefined)?.code
  if (err.response.status === 403) return 'not-an-admin'
  if (code === 'SIGN_IN_EXPIRED' || code === 'LINK_INVALID') return 'expired'
  if (err.response.status === 401) return 'signed-out'
  // Anything else (400, 404, 409, 500) is the server declining this one request: the session is still good.
  return 'refused'
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

// --- campaigns ----------------------------------------------------------------------------------------------

export type CampaignStatus = 'running' | 'scheduled' | 'ended' | 'disabled'

export interface AdminCampaign {
  id: number
  quizModeId: number
  quizModeTitle: string
  bookId: number | null
  bookTitle: string | null
  /** yyyy-MM-dd */
  startDate: string
  /** yyyy-MM-dd */
  endDate: string
  passingScore: number
  rewardTitle: string
  imageQuestionsPerQuiz: number
  isEnabled: boolean
  status: CampaignStatus
  attemptsStarted: number
  attemptsCompleted: number
  /** Plain-language problems, already in Azerbaijani. */
  issues: string[]
}

export interface CampaignOptions {
  /** The server's today (yyyy-MM-dd): what "running" is measured against. */
  today: string
  quizModes: { id: number; title: string; isActive: boolean; requiresBook: boolean }[]
  books: { id: number; title: string; author: string; isActive: boolean }[]
  pools: { quizModeId: number; bookId: number | null; easy: number; medium: number; hard: number; images: number }[]
  easyPerQuiz: number
  mediumPerQuiz: number
  hardPerQuiz: number
}

export interface CampaignInput {
  quizModeId: number | null
  bookId: number | null
  startDate: string
  endDate: string
  passingScore: number
  rewardTitle: string
  imageQuestionsPerQuiz: number
  isEnabled: boolean
}

/** Field → messages from a refused save (400 CAMPAIGN_INVALID), or null when the failure was something else. */
export function campaignErrors(err: unknown): Record<string, string[]> | null {
  if (!isAxiosError(err) || err.response?.status !== 400) return null
  const data = err.response.data as { code?: string; errors?: Record<string, string[]> } | undefined
  return data?.code === 'CAMPAIGN_INVALID' && data.errors ? data.errors : null
}

export async function getCampaigns(signal?: AbortSignal): Promise<AdminCampaign[]> {
  const { data } = await api.get<AdminCampaign[]>('/api/admin/campaigns', { signal })
  return data
}

export async function getCampaignOptions(signal?: AbortSignal): Promise<CampaignOptions> {
  const { data } = await api.get<CampaignOptions>('/api/admin/campaigns/options', { signal })
  return data
}

export async function createCampaign(input: CampaignInput): Promise<AdminCampaign> {
  const { data } = await api.post<AdminCampaign>('/api/admin/campaigns', input, { headers: ADMIN_HEADERS })
  return data
}

export async function updateCampaign(id: number, input: CampaignInput): Promise<AdminCampaign> {
  const { data } = await api.put<AdminCampaign>(`/api/admin/campaigns/${id}`, input, { headers: ADMIN_HEADERS })
  return data
}

export function campaignInput(campaign: AdminCampaign): CampaignInput {
  return {
    quizModeId: campaign.quizModeId,
    bookId: campaign.bookId,
    startDate: campaign.startDate,
    endDate: campaign.endDate,
    passingScore: campaign.passingScore,
    rewardTitle: campaign.rewardTitle,
    imageQuestionsPerQuiz: campaign.imageQuestionsPerQuiz,
    isEnabled: campaign.isEnabled,
  }
}

// --- results and participants -------------------------------------------------------------------------------

export interface AttemptRow {
  attemptId: number
  participantId: number
  /** Null for an unfinished attempt. */
  rank: number | null
  fullName: string
  /** Masked by the server; full numbers are only in the Excel export. */
  phone: string
  startedAtUtc: string
  completedAtUtc: string | null
  correctAnswers: number | null
  totalQuestions: number
  pointsEarned: number | null
  maxPoints: number
  durationSeconds: number | null
  passed: boolean | null
  /** This participant's attempts in every campaign: what removing them deletes. */
  participantAttempts: number
}

export interface CampaignResults {
  campaign: {
    id: number
    quizModeTitle: string
    bookTitle: string | null
    startDate: string
    endDate: string
    passingScore: number
    rewardTitle: string
  }
  started: number
  completed: number
  passed: number
  unfinished: number
  attempts: AttemptRow[]
}

export async function getCampaignResults(campaignId: number, search: string, signal?: AbortSignal): Promise<CampaignResults> {
  const { data } = await api.get<CampaignResults>(`/api/admin/campaigns/${campaignId}/results`, {
    params: search ? { search } : undefined,
    signal,
  })
  return data
}

/** A plain link: the browser downloads it with the session cookie. The server records the export. */
export function resultsExportUrl(campaignId: number): string {
  return `${api.defaults.baseURL ?? ''}/api/admin/campaigns/${campaignId}/results.xlsx`
}

export async function resetAttempt(attemptId: number, reason: string): Promise<void> {
  await api.post(`/api/admin/attempts/${attemptId}/reset`, { reason }, { headers: ADMIN_HEADERS })
}

export async function removeParticipant(participantId: number, reason: string): Promise<void> {
  await api.post(`/api/admin/participants/${participantId}/remove`, { reason }, { headers: ADMIN_HEADERS })
}

/** The server's own explanation of a refused correction (REASON_REQUIRED, ATTEMPT_IN_PLAY, ...), when there is one. */
export function refusalMessage(err: unknown): string | null {
  if (!isAxiosError(err) || !err.response || err.response.status === 401 || err.response.status === 403) return null
  const detail = (err.response.data as { detail?: string } | undefined)?.detail
  return typeof detail === 'string' && detail.length > 0 ? detail : null
}
