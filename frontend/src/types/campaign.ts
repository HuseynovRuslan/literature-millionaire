/** Book shown on the campaign page. Mirrors CampaignBookDto. */
export interface CampaignBook {
  id: number
  title: string
  author: string
  description: string
  /** Relative (e.g. /covers/oluler.webp) or absolute URL. May point at a file that does not exist yet. */
  coverImageUrl: string
}

/** Mirrors QuizModeDto from GET /api/campaigns/available. iconKey is a local-icon lookup key, never a URL. */
export interface QuizMode {
  id: number
  slug: string
  title: string
  description: string
  iconKey: string
  displayOrder: number
  /** The bank is playable but unfinished; the card says so. Set in the panel, not in this code. */
  isPreview: boolean
}

/** Mirrors QuizModeRefDto: the minimal identity carried by game start/result/leaderboard responses. */
export interface QuizModeRef {
  id: number
  slug: string
  title: string
}

/**
 * Mirrors CampaignSummaryDto from GET /api/campaigns/available: one playable campaign, as listed on
 * the category selection screen. `book` is null for quiz modes that are not played from a book.
 */
export interface CampaignSummary {
  campaignId: number
  startDate: string
  endDate: string
  passingScore: number
  rewardTitle: string
  questionCount: number
  imageQuestionsPerQuiz: number
  quizMode: QuizMode
  book: CampaignBook | null
}
