/** Book shown on the campaign page. Mirrors CampaignBookDto. */
export interface CampaignBook {
  id: number
  title: string
  author: string
  description: string
  /** Relative (e.g. /covers/oluler.webp) or absolute URL. May point at a file that does not exist yet. */
  coverImageUrl: string
}

/** Mirrors CurrentCampaignDto from GET /api/campaigns/current. Dates are ISO calendar dates (YYYY-MM-DD). */
export interface CurrentCampaign {
  campaignId: number
  startDate: string
  endDate: string
  passingScore: number
  rewardTitle: string
  questionCount: number
  book: CampaignBook
}
