import type { CampaignSummary } from '../../types/campaign'
import { formatDateRange } from '../../utils/date'
import { CARD_PRIMARY_CTA, CARD_SECONDARY_CTA } from './gameShowClasses'
import QuizModeIcon from './QuizModeIcon'

/**
 * One playable category/campaign. Every displayed value comes from the `available` API response;
 * nothing here is hardcoded. Not a button itself (screen readers get a labelled article containing
 * two real buttons), so there is never a nested interactive element.
 */
export default function CategoryCard({
  campaign,
  onSelect,
  onLeaderboard,
}: {
  campaign: CampaignSummary
  onSelect: () => void
  onLeaderboard: () => void
}) {
  const { quizMode, book } = campaign
  const titleId = `category-${campaign.campaignId}-title`

  return (
    <article
      aria-labelledby={titleId}
      data-testid="category-card"
      data-campaign-id={campaign.campaignId}
      className="home-stage rise flex h-full min-w-0 flex-col gap-[clamp(0.4rem,0.9vh,0.7rem)] short:gap-[0.35rem] rounded-[clamp(1rem,1.3vw,1.5rem)] px-[clamp(1rem,1.5vw,1.5rem)] py-[clamp(0.8rem,1.5vh,1.2rem)] short:py-2.5 max-sm:rounded-2xl max-sm:px-4 max-sm:py-4"
    >
      <div className="flex items-center gap-[clamp(0.55rem,0.85vw,0.85rem)]">
        <span
          aria-hidden="true"
          className="grid size-[clamp(2.3rem,3.2vw,3rem)] shrink-0 place-items-center rounded-full bg-[rgba(243,215,126,0.14)] text-[var(--p-gold-light)] ring-1 ring-[rgba(233,192,105,0.55)]"
        >
          <QuizModeIcon iconKey={quizMode.iconKey} className="size-[56%]" />
        </span>
        <div className="min-w-0">
          <h2 id={titleId} lang="az" className="truncate font-display text-[clamp(1.1rem,1.55vw,1.5rem)] font-bold leading-tight text-[#fbf6ec]">
            {quizMode.title}
          </h2>
          <p className="truncate text-[clamp(0.68rem,0.78vw,0.82rem)] font-semibold uppercase tracking-[0.09em] text-[var(--p-gold-light)]">
            {formatDateRange(campaign.startDate, campaign.endDate)}
          </p>
        </div>
      </div>

      <p lang="az" className="line-clamp-2 short:line-clamp-1 text-[clamp(0.82rem,0.95vw,0.98rem)] leading-snug text-[#d6deec]">
        {quizMode.description}
      </p>

      {book && (
        <p lang="az" className="truncate text-[clamp(0.76rem,0.86vw,0.9rem)] text-[#c9d3e6]">
          <span className="text-[var(--p-gold-light)]">Kitab: </span>
          {book.title}
          {book.author.trim() ? ` — ${book.author.trim()}` : ''}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-x-2.5 gap-y-0.5 text-[clamp(0.74rem,0.84vw,0.9rem)] text-[#c9d3e6]" data-testid="category-rules">
        <span>{campaign.questionCount} sual</span>
        <span aria-hidden="true">·</span>
        <span>
          Keçid: {campaign.passingScore}/{campaign.questionCount}
        </span>
        <span aria-hidden="true">·</span>
        <span>1 iştirakçı — 1 cəhd</span>
      </div>

      {campaign.rewardTitle.trim() && (
        <p lang="az" className="truncate text-[clamp(0.78rem,0.88vw,0.92rem)] font-semibold text-[var(--p-gold-light)]" data-testid="category-reward">
          Mükafat: {campaign.rewardTitle}
        </p>
      )}

      <div className="mt-auto flex items-stretch gap-[clamp(0.5rem,0.8vw,0.8rem)] pt-[clamp(0.3rem,0.6vh,0.5rem)] max-sm:flex-col max-sm:gap-2.5">
        <button type="button" onClick={onSelect} className={`${CARD_PRIMARY_CTA} flex-[1.6]`} data-testid="category-select">
          SEÇ VƏ DAVAM ET
        </button>
        <button type="button" onClick={onLeaderboard} className={`${CARD_SECONDARY_CTA} flex-1`} data-testid="category-leaderboard">
          LİDER CƏDVƏLİ
        </button>
      </div>
    </article>
  )
}
