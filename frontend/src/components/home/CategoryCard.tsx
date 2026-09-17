import type { CSSProperties } from 'react'
import type { CampaignSummary } from '../../types/campaign'
import { formatDateRange } from '../../utils/date'
import { ClockIcon, ListIcon, PlayIcon, RewardMedal, TargetIcon, TrophyIcon } from './GameShowArt'
import { CARD_PRIMARY_CTA, CARD_QUIET_CTA } from './gameShowClasses'
import { CarpetBand } from '../arena/NationalMotifs'
import QuizModeIcon from './QuizModeIcon'
import CategoryArt from './CategoryArt'
import { accentFor, bookCategories, previewCategories } from './categoryThemes'

/**
 * One playable category/campaign. Every displayed value comes from the `available` API response; the only
 * text written here is the labels and the one-attempt rule, which is QuizRules.MaxAttemptsPerCampaign on the
 * server. Not a button itself (screen readers get a labelled article containing two real buttons), so there
 * is never a nested interactive element.
 *
 * Read in this order, and styled so the eye follows it: what the category is (the title on its colour), what
 * it is about (the description, and for a book mode the book), what a round is (one quiet line of facts),
 * then the one thing to do - "Oyna". The reward and the leaderboard are there for whoever looks for them,
 * and neither competes with the button. It used to be eight items of equal weight - three chips, a reward
 * slab and two equal buttons - and it read like an admin table.
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
  // The category's own colour, not the one its position in the grid happened to land on.
  const accent = accentFor(quizMode.slug, index)
  const isPreview = previewCategories.has(quizMode.slug)
  // Only a mode played from one book names it; for the others `book` is the question bank's container.
  const namedBook = book && bookCategories.has(quizMode.slug) && book.title.trim() ? book : null

  return (
    <article
      aria-labelledby={titleId}
      data-testid="category-card"
      data-campaign-id={campaign.campaignId}
      style={{ '--accent': accent, animationDelay: `${80 + index * 70}ms` } as CSSProperties}
      className="card card-lift rise relative isolate flex h-full min-w-0 flex-col overflow-hidden rounded-[1.75rem] shadow-[0_1.5rem_3rem_-1.5rem_rgba(4,2,18,0.8),0_0_0_1px_color-mix(in_srgb,var(--accent)_28%,transparent),0_1.2rem_2.6rem_-1.6rem_color-mix(in_srgb,var(--accent)_45%,transparent)] max-sm:rounded-3xl"
    >
      {/* The category's colour as a hairline along the top edge: present at a glance, quiet up close. */}
      <span aria-hidden="true" className="absolute inset-x-0 top-0 z-10 h-[3px] bg-[linear-gradient(90deg,transparent,color-mix(in_srgb,var(--accent)_85%,#ffffff),transparent)]" />
      {/*
        Top zone: the header band and what the category is about, and the only place the category picture
        can be. Everything below it (facts, reward, buttons) is a separate zone the picture cannot reach, so
        no tuning of sizes can ever put it over something a player has to read or press. Text in this zone
        keeps clear of the right-hand side with card-art-safe.
      */}
      <div className="card-top relative">
        <CategoryArt slug={quizMode.slug} book={book} />

        {/* Coloured header band with the category icon. The colour is its own layer (z-0) so the picture
            (z-1) passes over the band and under the title (z-10) - breaking out of the band, never over text. */}
        <div className="card-band relative flex items-center gap-4 px-[clamp(1.1rem,1.6vw,1.6rem)] py-[clamp(1rem,1.6vh,1.3rem)]">
          <span aria-hidden="true" className="absolute inset-0 z-0 bg-[linear-gradient(135deg,color-mix(in_srgb,var(--accent)_92%,#ffffff),color-mix(in_srgb,var(--accent)_72%,#110c2c))]" />
          <span aria-hidden="true" className="wiggle relative z-10 grid size-[clamp(3.2rem,4.2vw,4rem)] shrink-0 place-items-center rounded-2xl bg-[color-mix(in_srgb,var(--accent)_45%,#ffffff_28%)] text-white ring-1 ring-white/30 shadow-[inset_0_1px_0_rgba(255,255,255,0.35)] max-sm:size-12">
            <QuizModeIcon iconKey={quizMode.iconKey} className="size-[58%]" />
          </span>
          <div className="card-art-safe relative z-10 min-w-0 flex-1">
            <h2 id={titleId} lang="az" className="font-display text-[clamp(1.1rem,1.5vw,1.5rem)] font-bold leading-tight text-white [text-wrap:balance]">
              {quizMode.title}
            </h2>
            {/* The dates matter less than the name: smaller, lighter, and the test status rides on this line
                as a small tag rather than a block of its own. */}
            <p className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[clamp(0.76rem,0.84vw,0.84rem)] font-semibold text-white/75">
              <span className="flex items-center gap-1.5">
                <ClockIcon className="size-[1.05em] shrink-0" />
                {formatDateRange(campaign.startDate, campaign.endDate)}
              </span>
              {isPreview && (
                <span data-testid="category-preview" className="rounded-full bg-white/90 px-1.5 py-px font-display text-[0.66rem] font-extrabold uppercase tracking-[0.1em] text-ink-950">
                  Test versiya
                </span>
              )}
            </p>
          </div>
          <CarpetBand className="absolute inset-x-0 bottom-0 z-0 text-white/30" />
          {/* Where the coloured header meets the dark body: the seam is lit in the category's own colour. */}
          <span aria-hidden="true" className="absolute inset-x-0 bottom-0 z-0 h-px bg-[color-mix(in_srgb,var(--accent)_40%,#ffffff)] opacity-60" />
        </div>

        <div className="card-art-safe card-top-body relative z-10 flex flex-col gap-3 px-[clamp(1.1rem,1.6vw,1.6rem)] pt-3.5">
          {/* Short: it says what the category is about, not everything in it. Two lines on a phone, where it
              has the card's full width; three where it shares its row with the picture and each line is about
              half as long - two there cut "bilik yarışı." down to "bilik…". A screen reader still reads it all. */}
          <p lang="az" className="line-clamp-3 text-[clamp(0.92rem,0.98vw,0.98rem)] font-medium leading-relaxed text-fg-2 max-sm:line-clamp-2">
            {quizMode.description}
          </p>

          {/* The book a book mode is played from: the name that matters here, above any number. */}
          {namedBook && (
            <p lang="az" className="min-w-0 leading-tight" data-testid="category-book">
              <span className="sr-only">Kitab: </span>
              <span className="block truncate font-display text-[clamp(1rem,1.1vw,1.1rem)] font-bold text-fg">{namedBook.title}</span>
              {namedBook.author.trim() && (
                <span className="mt-1 block truncate text-[clamp(0.84rem,0.9vw,0.9rem)] font-medium text-fg-3">{namedBook.author.trim()}</span>
              )}
            </p>
          )}

          {isPreview && (
            <p lang="az" className="text-[clamp(0.78rem,0.84vw,0.84rem)] leading-snug text-fg-3">
              Bu kateqoriya hazırlanır — suallar və adlar dəyişə bilər.
            </p>
          )}
        </div>
      </div>

      {/* Bottom zone: full width, and out of the picture's reach by construction. */}
      <div className="relative z-10 flex flex-1 flex-col gap-3 px-[clamp(1.1rem,1.6vw,1.6rem)] pb-[clamp(0.9rem,1.3vw,1.2rem)] pt-3">
        {/* What a round is, as one quiet line of facts rather than three chips that each ask to be read. */}
        <ul className="card-facts flex flex-wrap items-center gap-y-1 text-[clamp(0.84rem,0.9vw,0.9rem)] font-semibold text-fg-2" data-testid="category-rules">
          <li className="flex items-center gap-1.5"><ListIcon className="size-[1.05em] text-fg-3" />{campaign.questionCount} sual</li>
          <li className="flex items-center gap-1.5"><TargetIcon className="size-[1.05em] text-fg-3" />Keçid {campaign.passingScore}/{campaign.questionCount}</li>
          <li>1 cəhd</li>
        </ul>

        {campaign.rewardTitle.trim() && (
          <p lang="az" className="flex items-center gap-2 rounded-xl border border-sun/20 bg-sun/[0.06] px-2.5 py-1.5 text-[clamp(0.82rem,0.88vw,0.88rem)] font-semibold leading-snug text-sun/90" data-testid="category-reward">
            <RewardMedal className="size-5 shrink-0" />
            <span className="line-clamp-2"><span className="sr-only">Mükafat: </span>{campaign.rewardTitle}</span>
          </p>
        )}

        {/* One obvious action. "Liderlər" sits beside it as a quiet text button, and under it on a phone. */}
        <div className="mt-auto flex items-center gap-2 pt-1.5 max-sm:flex-col max-sm:items-stretch max-sm:gap-1">
          <button type="button" onClick={onSelect} className={`${CARD_PRIMARY_CTA} flex-1`} data-testid="category-select">
            <PlayIcon className="size-[0.95em] shrink-0" />
            Oyna
          </button>
          <button type="button" onClick={onLeaderboard} className={CARD_QUIET_CTA} data-testid="category-leaderboard">
            <TrophyIcon className="size-[1.05em] shrink-0" />
            Liderlər
          </button>
        </div>
      </div>
    </article>
  )
}
