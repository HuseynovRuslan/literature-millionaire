import axios, { isAxiosError } from 'axios'
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

// --- uploaded pictures --------------------------------------------------------------------------------------

export type ImageKind = 'question' | 'cover'

export interface UploadedImage {
  id: number
  kind: ImageKind
  /** Site path, e.g. "/uploads/questions/0123….webp". */
  url: string
  width: number
  height: number
  bytes: number
  originalFileName: string
  uploadedAtUtc: string
  uploadedBy: string
  /** True when this exact picture was already on the server: nothing new was stored. */
  alreadyExisted: boolean
}

/** What the server accepts. It re-encodes everything to WebP itself. */
export const UPLOAD_ACCEPT = 'image/jpeg,image/png,image/webp'
export const UPLOAD_MAX_BYTES = 10 * 1024 * 1024

export async function getUploads(kind: ImageKind, signal?: AbortSignal): Promise<UploadedImage[]> {
  const { data } = await api.get<UploadedImage[]>('/api/admin/uploads', { params: { kind, take: 60 }, signal })
  return data
}

export async function uploadImage(file: File, kind: ImageKind): Promise<UploadedImage> {
  const form = new FormData()
  form.append('file', file)
  form.append('kind', kind)
  // Deliberately not the shared `api` client: its default Content-Type (application/json) would replace the
  // multipart content type the browser has to set, boundary and all, and the server would see no file at all.
  // Same origin, so the admin session cookie rides along by itself.
  const { data } = await axios.post<UploadedImage>(`${api.defaults.baseURL ?? ''}/api/admin/uploads`, form, {
    headers: ADMIN_HEADERS,
    timeout: 60_000,
  })
  return data
}

// --- books --------------------------------------------------------------------------------------------------

export interface AdminBook {
  id: number
  title: string
  author: string
  description: string
  /** A picture from the library, or "" for none. */
  coverImageUrl: string
  isActive: boolean
  questionCount: number
  campaignCount: number
  campaigns: { id: number; quizModeTitle: string; startDate: string; endDate: string; isEnabled: boolean }[]
}

export interface BookInput {
  title: string
  author: string
  description: string
  coverImageUrl: string
  isActive: boolean
}

/** Field → messages from a refused save (400 BOOK_INVALID), or null when the failure was something else. */
export function bookErrors(err: unknown): Record<string, string[]> | null {
  if (!isAxiosError(err) || err.response?.status !== 400) return null
  const data = err.response.data as { code?: string; errors?: Record<string, string[]> } | undefined
  return data?.code === 'BOOK_INVALID' && data.errors ? data.errors : null
}

export async function getBooks(signal?: AbortSignal): Promise<AdminBook[]> {
  const { data } = await api.get<AdminBook[]>('/api/admin/books', { signal })
  return data
}

export async function createBook(input: BookInput): Promise<AdminBook> {
  const { data } = await api.post<AdminBook>('/api/admin/books', input, { headers: ADMIN_HEADERS })
  return data
}

export async function updateBook(id: number, input: BookInput): Promise<AdminBook> {
  const { data } = await api.put<AdminBook>(`/api/admin/books/${id}`, input, { headers: ADMIN_HEADERS })
  return data
}

export function bookInput(book: AdminBook): BookInput {
  return {
    title: book.title,
    author: book.author,
    description: book.description,
    coverImageUrl: book.coverImageUrl,
    isActive: book.isActive,
  }
}

// --- questions ----------------------------------------------------------------------------------------------

export type Difficulty = 'Easy' | 'Medium' | 'Hard'

export interface AdminQuestion {
  id: number
  text: string
  optionA: string
  optionB: string
  optionC: string
  optionD: string
  /** "A" | "B" | "C" | "D" */
  correctOption: string
  difficulty: Difficulty
  category: string
  explanation: string | null
  bookId: number | null
  bookTitle: string | null
  quizModeId: number | null
  quizModeTitle: string | null
  imageUrl: string | null
  imageAltText: string | null
  imageSource: string | null
  imageLicense: string | null
  createdAt: string
}

export interface QuestionPage {
  total: number
  skip: number
  take: number
  questions: AdminQuestion[]
}

export interface QuestionOptions {
  banks: { id: number; title: string; isActive: boolean; easy: number; medium: number; hard: number; images: number }[]
  quizModes: { id: number; title: string; isActive: boolean; requiresBook: boolean }[]
  /** Sub-categories already in use, so the form suggests instead of asking. */
  categories: string[]
  easyPerQuiz: number
  mediumPerQuiz: number
  hardPerQuiz: number
}

export interface QuestionInput {
  text: string
  optionA: string
  optionB: string
  optionC: string
  optionD: string
  correctOption: string
  difficulty: Difficulty
  category: string
  explanation: string
  bookId: number | null
  quizModeId: number | null
  imageUrl: string
  imageAltText: string
  imageSource: string
  imageLicense: string
}

export interface QuestionFilter {
  bookId: number | null
  quizModeId: number | null
  difficulty: Difficulty | null
  withImage: boolean | null
  search: string
  skip: number
  take: number
}

/** Field → messages from a refused save (400 QUESTION_INVALID), or null when the failure was something else. */
export function questionErrors(err: unknown): Record<string, string[]> | null {
  if (!isAxiosError(err) || err.response?.status !== 400) return null
  const data = err.response.data as { code?: string; errors?: Record<string, string[]> } | undefined
  return data?.code === 'QUESTION_INVALID' && data.errors ? data.errors : null
}

export async function getQuestions(filter: QuestionFilter, signal?: AbortSignal): Promise<QuestionPage> {
  const { data } = await api.get<QuestionPage>('/api/admin/questions', {
    params: {
      bookId: filter.bookId ?? undefined,
      quizModeId: filter.quizModeId ?? undefined,
      difficulty: filter.difficulty ?? undefined,
      withImage: filter.withImage ?? undefined,
      search: filter.search || undefined,
      skip: filter.skip,
      take: filter.take,
    },
    signal,
  })
  return data
}

export async function getQuestionOptions(signal?: AbortSignal): Promise<QuestionOptions> {
  const { data } = await api.get<QuestionOptions>('/api/admin/questions/options', { signal })
  return data
}

export async function createQuestion(input: QuestionInput): Promise<AdminQuestion> {
  const { data } = await api.post<AdminQuestion>('/api/admin/questions', input, { headers: ADMIN_HEADERS })
  return data
}

export async function updateQuestion(id: number, input: QuestionInput): Promise<AdminQuestion> {
  const { data } = await api.put<AdminQuestion>(`/api/admin/questions/${id}`, input, { headers: ADMIN_HEADERS })
  return data
}

export async function deleteQuestion(id: number): Promise<void> {
  await api.delete(`/api/admin/questions/${id}`, { headers: ADMIN_HEADERS })
}

export function questionInput(question: AdminQuestion): QuestionInput {
  return {
    text: question.text,
    optionA: question.optionA,
    optionB: question.optionB,
    optionC: question.optionC,
    optionD: question.optionD,
    correctOption: question.correctOption,
    difficulty: question.difficulty,
    category: question.category,
    explanation: question.explanation ?? '',
    bookId: question.bookId,
    quizModeId: question.quizModeId,
    imageUrl: question.imageUrl ?? '',
    imageAltText: question.imageAltText ?? '',
    imageSource: question.imageSource ?? '',
    imageLicense: question.imageLicense ?? '',
  }
}

// --- bulk import --------------------------------------------------------------------------------------------

export interface ImportRow {
  /** The row number in the file, as Excel shows it. */
  row: number
  status: 'ready' | 'duplicate' | 'problem'
  text: string
  difficulty: string | null
  category: string | null
  hasImage: boolean
  problems: string[]
}

export interface ImportReport {
  /** Sent back to apply exactly what this report describes. */
  token: string
  fileName: string
  bankTitle: string
  quizModeTitle: string
  totalRows: number
  ready: number
  duplicates: number
  problems: number
  columns: string[]
  rows: ImportRow[]
}

export interface ImportResult {
  added: number
  skipped: number
  bankTitle: string
}

export const IMPORT_ACCEPT = '.xlsx,.json'
export const IMPORT_MAX_BYTES = 5 * 1024 * 1024

export function importTemplateUrl(): string {
  return `${api.defaults.baseURL ?? ''}/api/admin/questions/import/template.xlsx`
}

/** Uploads the file and reports what it would do. Writes nothing. */
export async function analyseImport(file: File, bookId: number, quizModeId: number, category: string): Promise<ImportReport> {
  const form = new FormData()
  form.append('file', file)
  form.append('bookId', String(bookId))
  form.append('quizModeId', String(quizModeId))
  if (category) form.append('category', category)
  // Not the shared client: its JSON content type would replace the multipart one the browser has to set.
  const { data } = await axios.post<ImportReport>(`${api.defaults.baseURL ?? ''}/api/admin/questions/import`, form, {
    headers: ADMIN_HEADERS,
    timeout: 120_000,
  })
  return data
}

export async function applyImport(token: string): Promise<ImportResult> {
  const { data } = await api.post<ImportResult>('/api/admin/questions/import/apply', { token }, { headers: ADMIN_HEADERS })
  return data
}
