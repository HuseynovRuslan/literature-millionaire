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
      className="home-stage rise flex h-full min-w-0 flex-col gap-[0.4781rem] rounded-[clamp(1rem,1.3vw,1.17rem)] px-[clamp(1rem,1.5vw,1.35rem)] py-[0.8rem] max-sm:rounded-2xl max-sm:px-4 max-sm:py-4"
    >
      <div className="flex items-center gap-[clamp(0.55rem,0.85vw,0.765rem)]">
        <span
          aria-hidden="true"
          className="grid size-[clamp(2.3rem,3.2vw,2.88rem)] shrink-0 place-items-center rounded-full bg-[rgba(232,210,156,0.14)] text-[var(--p-gold-light)] ring-1 ring-[rgba(217,187,124,0.55)]"
        >
          <QuizModeIcon iconKey={quizMode.iconKey} className="size-[56%]" />
        </span>
        <div className="min-w-0">
          <h2 id={titleId} lang="az" className="truncate font-display text-[clamp(1.1rem,1.55vw,1.395rem)] font-bold leading-tight text-[#fbf6ec]">
            {quizMode.title}
          </h2>
          <p className="truncate text-[clamp(0.68rem,0.78vw,0.702rem)] font-semibold uppercase tracking-[0.09em] text-[var(--p-gold-light)]">
            {formatDateRange(campaign.startDate, campaign.endDate)}
          </p>
        </div>
      </div>

      {/* Two lines is enough once the grid reaches three columns; a narrower card needs a third. */}
      <p lang="az" className="line-clamp-3 text-[clamp(0.82rem,0.95vw,0.855rem)] leading-snug text-[#d8dbe3] xl:line-clamp-2">
        {quizMode.description}
      </p>

      {book && (
        <p lang="az" className="line-clamp-2 text-[clamp(0.76rem,0.86vw,0.774rem)] text-[#c2c7d3]">
          <span className="text-[var(--p-gold-light)]">Kitab: </span>
          {book.title}
          {book.author.trim() ? ` — ${book.author.trim()}` : ''}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-x-2.5 gap-y-0.5 text-[clamp(0.74rem,0.84vw,0.756rem)] text-[#c2c7d3]" data-testid="category-rules">
        <span>{campaign.questionCount} sual</span>
        <span aria-hidden="true">·</span>
        <span>
          Keçid: {campaign.passingScore}/{campaign.questionCount}
        </span>
        <span aria-hidden="true">·</span>
        <span>1 iştirakçı — 1 cəhd</span>
      </div>

      {campaign.rewardTitle.trim() && (
        <p lang="az" className="line-clamp-2 text-[clamp(0.78rem,0.88vw,0.792rem)] font-semibold text-[var(--p-gold-light)]" data-testid="category-reward">
          Mükafat: {campaign.rewardTitle}
        </p>
      )}

      <div className="mt-auto flex items-stretch gap-[clamp(0.5rem,0.8vw,0.72rem)] pt-[0.3188rem] max-sm:flex-col max-sm:gap-2.5">
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
