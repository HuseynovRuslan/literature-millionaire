import type { CSSProperties } from 'react'
import type { CampaignSummary } from '../../types/campaign'
import { formatDateRange } from '../../utils/date'
import { ClockIcon, ListIcon, PlayIcon, RewardMedal, TargetIcon, TrophyIcon } from './GameShowArt'
import { CARD_PRIMARY_CTA, CARD_SECONDARY_CTA } from './gameShowClasses'
import { CarpetBand } from '../arena/NationalMotifs'
import QuizModeIcon from './QuizModeIcon'

/** Each card gets its own accent from the answer palette, in display order, so the grid reads as a game board. */
const ACCENTS = ['var(--color-brand)', 'var(--color-opt-a)', 'var(--color-opt-d)', 'var(--color-opt-b)', 'var(--color-opt-c)']

/**
 * One playable category/campaign. Every displayed value comes from the `available` API response;
 * nothing here is hardcoded. Not a button itself (screen readers get a labelled article containing
 * two real buttons), so there is never a nested interactive element.
 */
export default function CategoryCard({
  campaign,
  index = 0,
  onSelect,
  onLeaderboard,
}: {
  campaign: CampaignSummary
  index?: number
  onSelect: () => void
  onLeaderboard: () => void
}) {
  const { quizMode, book } = campaign
  const titleId = `category-${campaign.campaignId}-title`
  const accent = ACCENTS[index % ACCENTS.length]

  return (
    <article
      aria-labelledby={titleId}
      data-testid="category-card"
      data-campaign-id={campaign.campaignId}
      style={{ '--accent': accent, animationDelay: `${80 + index * 70}ms` } as CSSProperties}
      className="card card-lift rise flex h-full min-w-0 flex-col overflow-hidden rounded-[1.75rem] max-sm:rounded-3xl"
    >
      {/* Coloured header band with the category icon. */}
      <div className="relative flex items-center gap-4 bg-[linear-gradient(135deg,color-mix(in_srgb,var(--accent)_92%,#ffffff),color-mix(in_srgb,var(--accent)_72%,#110c2c))] px-[clamp(1.1rem,1.6vw,1.6rem)] py-[clamp(1rem,1.6vh,1.3rem)]">
        <span aria-hidden="true" className="wiggle grid size-[clamp(3.2rem,4.2vw,4rem)] shrink-0 place-items-center rounded-2xl bg-white/20 text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.35)] max-sm:size-12">
          <QuizModeIcon iconKey={quizMode.iconKey} className="size-[58%]" />
        </span>
        <div className="min-w-0">
          <h2 id={titleId} lang="az" className="font-display text-[clamp(1.1rem,1.5vw,1.5rem)] font-bold leading-tight text-white [text-wrap:balance]">
            {quizMode.title}
          </h2>
          <p className="mt-1 flex items-center gap-1.5 text-[clamp(0.8rem,0.9vw,0.9rem)] font-bold text-white/90">
            <ClockIcon className="size-[1.1em] shrink-0" />
            {formatDateRange(campaign.startDate, campaign.endDate)}
          </p>
        </div>
        <CarpetBand className="absolute inset-x-0 bottom-0 text-white/30" />
      </div>

      <div className="flex flex-1 flex-col gap-3 px-[clamp(1.1rem,1.6vw,1.6rem)] pb-[clamp(1.1rem,1.6vw,1.5rem)] pt-4">
        <p lang="az" className="line-clamp-3 text-[clamp(0.95rem,1.02vw,1.02rem)] font-medium leading-relaxed text-fg-2">
          {quizMode.description}
        </p>

        {book && (
          <p lang="az" className="line-clamp-2 text-[clamp(0.9rem,0.95vw,0.95rem)] text-fg-2">
            <span className="font-bold text-brand-soft">Kitab: </span>
            {book.title}
            {book.author.trim() ? ` — ${book.author.trim()}` : ''}
          </p>
        )}

        <ul className="flex flex-wrap gap-2 text-[clamp(0.8rem,0.86vw,0.86rem)]" data-testid="category-rules">
          <li className="chip px-3 py-1.5"><ListIcon className="size-[1.1em]" />{campaign.questionCount} sual</li>
          <li className="chip px-3 py-1.5"><TargetIcon className="size-[1.1em]" />Keçid: {campaign.passingScore}/{campaign.questionCount}</li>
          <li className="chip px-3 py-1.5">1 iştirakçı — 1 cəhd</li>
        </ul>

        {campaign.rewardTitle.trim() && (
          <p lang="az" className="flex items-center gap-2.5 rounded-2xl bg-sun/10 px-3 py-2 text-[clamp(0.9rem,0.95vw,0.95rem)] font-bold text-sun ring-1 ring-sun/25" data-testid="category-reward">
            <RewardMedal className="size-7 shrink-0" />
            <span className="line-clamp-2">Mükafat: {campaign.rewardTitle}</span>
          </p>
        )}

        <div className="mt-auto flex items-stretch gap-3 pt-2 max-sm:flex-col">
          <button type="button" onClick={onSelect} className={`${CARD_PRIMARY_CTA} flex-[1.6]`} data-testid="category-select">
            <PlayIcon className="size-[0.95em] shrink-0" />
            Oyna
          </button>
          <button type="button" onClick={onLeaderboard} className={`${CARD_SECONDARY_CTA} flex-1`} data-testid="category-leaderboard">
            <TrophyIcon className="size-[1.1em] shrink-0" />
            Liderlər
          </button>
        </div>
      </div>
    </article>
  )
}
