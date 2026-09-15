/** Privacy-safe leaderboard entry returned by the campaign leaderboard API. */
export interface LeaderboardEntry {
  rank: number
  displayName: string
  pointsEarned: number
  maxPoints: number
  correctAnswers: number
  totalQuestions: number
  durationSeconds: number
  /** ISO 8601 UTC. */
  completedAtUtc: string
}

export interface Leaderboard {
  campaignId: number
  /** ISO 8601 UTC generation timestamp. */
  generatedAtUtc: string
  entries: LeaderboardEntry[]
}
