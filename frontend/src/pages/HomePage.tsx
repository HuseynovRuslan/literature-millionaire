import { useEffect, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { classifyCampaignError, getCurrentCampaign, type CampaignErrorKind } from '../api/campaigns'
import { PlayIcon, QuizEmblem, RewardMedal } from '../components/home/GameShowArt'
import HomeHeader from '../components/home/HomeHeader'
import { BRAND_NAME } from '../components/national/BrandMark'
import CarpetFrame from '../components/national/CarpetFrame'
import { useGame } from '../game/GameContext'
import type { CampaignBook, CurrentCampaign } from '../types/campaign'

const MONTHS_AZ = ['yanvar', 'fevral', 'mart', 'aprel', 'may', 'iyun', 'iyul', 'avqust', 'sentyabr', 'oktyabr', 'noyabr', 'dekabr']

/** "2026-09-01".."2026-09-30" -> "1–30 sentyabr 2026"; across months -> "25 avqust – 5 oktyabr 2026". */
function formatRange(startIso: string, endIso: string): string {
  const [sy, sm, sd] = startIso.split('-').map(Number)
  const [ey, em, ed] = endIso.split('-').map(Number)
  if (![sy, sm, sd, ey, em, ed].every(Number.isFinite)) return `${startIso} – ${endIso}`
  if (sy === ey && sm === em) return `${sd}–${ed} ${MONTHS_AZ[sm - 1]} ${sy}`
  if (sy === ey) return `${sd} ${MONTHS_AZ[sm - 1]} – ${ed} ${MONTHS_AZ[em - 1]} ${sy}`
  return `${sd} ${MONTHS_AZ[sm - 1]} ${sy} – ${ed} ${MONTHS_AZ[em - 1]} ${ey}`
}

const ERROR_COPY: Record<CampaignErrorKind, { title: string; text: string }> = {
  'no-active': { title: 'Hazırda aktiv kampaniya yoxdur', text: '"Bilik yarışı" tezliklə yenidən başlayacaq.' },
  multiple: { title: 'Kampaniya tənzimlənməsində xəta var', text: 'Eyni vaxtda bir neçə kampaniya aktivdir. Zəhmət olmasa, inzibatçıya müraciət edin.' },
  unavailable: { title: 'Serverlə əlaqə yoxdur', text: 'Şəbəkə bağlantısını yoxlayın və yenidən cəhd edin.' },
  unexpected: { title: 'Gözlənilməz xəta baş verdi', text: 'Bir az sonra yenidən cəhd edin.' },
}

type CampaignLoad =
  | { kind: 'loading' }
  | { kind: 'ready'; data: CurrentCampaign }
  | { kind: 'error'; error: CampaignErrorKind }

/** The one action the screen is about: large, burgundy, touch-friendly (>= 88px on the kiosk). */
const PRIMARY =
  'tap paper-cta flex min-h-[clamp(5.5rem,11vh,7.5rem)] items-center justify-center gap-[clamp(0.8rem,1.2vw,1.2rem)] rounded-full px-[clamp(2rem,3vw,3.5rem)] font-display text-[clamp(2.1rem,3.1vw,3.6rem)] font-bold tracking-[0.06em] max-sm:min-h-[3.75rem] max-sm:flex-none max-sm:px-6 max-sm:text-[1.65rem]'
/** Same height as the primary so the row stays tidy, but narrower, lighter and in a smaller type. */
const SECONDARY =
  'tap paper-ghost flex min-h-[clamp(5.5rem,11vh,7.5rem)] items-center justify-center rounded-full px-[clamp(1.5rem,2.2vw,2.5rem)] font-display text-[clamp(1.3rem,1.75vw,2rem)] font-semibold tracking-[0.05em] max-sm:min-h-[3.25rem] max-sm:flex-none max-sm:text-[1.2rem]'

/** Shows the author only when it adds something: not empty and not just the organisation already in the header. */
function meaningfulAuthor(author: string): string | null {
  const value = author.trim()
  if (!value || value.toLocaleLowerCase('az') === BRAND_NAME.toLocaleLowerCase('az')) return null
  return value
}

/** Campaign cover when the API provides one that loads; otherwise the neutral quiz emblem. */
function CampaignArt({ book }: { book: CampaignBook }) {
  const [failed, setFailed] = useState(false)
  const cover = book.coverImageUrl.trim()
  if (cover && !failed) {
    return (
      <div className="self-center justify-self-center [grid-area:art]" data-testid="campaign-art" data-kind="cover">
        <img
          src={cover}
          alt={`"${book.title}" üz qabığı`}
          onError={() => setFailed(true)}
          className="aspect-[2/3] h-[clamp(15rem,min(22vw,42vh),27rem)] w-auto rounded-xl border-4 border-[var(--p-gold-light)] object-cover shadow-[0_1.2rem_2.4rem_-0.8rem_rgba(0,0,0,0.55)] max-lg:h-[12rem] max-sm:h-[7.5rem] max-sm:border-2"
        />
      </div>
    )
  }
  return (
    <div className="self-center justify-self-center [grid-area:art]" data-testid="campaign-art" data-kind="emblem">
      <QuizEmblem className="size-[clamp(15rem,min(22vw,42vh),27rem)] max-lg:size-[11rem] max-sm:size-[5.75rem]" />
    </div>
  )
}

/** Paper page with the carpet border, the brand bar and a vertically centred content column. */
function HomeShell({ children }: { children: ReactNode }) {
  return (
    <main className="kiosk kiosk-scroll paper flex flex-col">
      <CarpetFrame />
      <HomeHeader />
      {/* safe center: content taller than the space grows downwards instead of sliding under the header */}
      <div className="relative z-0 mx-auto flex min-h-0 w-full max-w-[118rem] flex-1 flex-col [justify-content:safe_center] gap-[clamp(1rem,2.6vh,2.2rem)] px-[calc(var(--frame)_+_2rem)] pb-[calc(var(--frame)_+_1rem)] pt-[clamp(0.6rem,1.6vh,1.4rem)] max-sm:justify-start max-sm:gap-4 max-sm:px-[calc(var(--frame)_+_0.75rem)] max-sm:pb-[calc(var(--frame)_+_1rem_+_var(--safe-bottom))] max-sm:pt-3">
        {children}
      </div>
    </main>
  )
}

/** Loading and error states use the same stage, centred. */
function StatusStage({ children, alert = false }: { children: ReactNode; alert?: boolean }) {
  return (
    <section
      role={alert ? 'alert' : 'status'}
      aria-live={alert ? undefined : 'polite'}
      data-testid="home-stage"
      className="home-stage rise flex min-h-[clamp(20rem,52vh,36rem)] flex-col items-center justify-center rounded-[clamp(1.2rem,1.6vw,2rem)] px-[clamp(1.5rem,4vw,5rem)] py-[clamp(2rem,5vh,4rem)] text-center max-sm:min-h-[22rem] max-sm:rounded-2xl max-sm:px-5 max-sm:py-8"
    >
      {children}
    </section>
  )
}

export default function HomePage() {
  const navigate = useNavigate()
  const { state, reset } = useGame()
  const [campaign, setCampaign] = useState<CampaignLoad>({ kind: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    getCurrentCampaign()
      .then((data) => { if (!cancelled) setCampaign({ kind: 'ready', data }) })
      .catch((err: unknown) => { if (!cancelled) setCampaign({ kind: 'error', error: classifyCampaignError(err) }) })
    return () => { cancelled = true }
  }, [attempt])

  function retry() {
    setCampaign({ kind: 'loading' })
    setAttempt((a) => a + 1)
  }

  // A valid, unanswered question restored from sessionStorage: offer to continue instead of starting a second session.
  const activeGame = state.status === 'playing' && state.question ? state : null

  // Registration happens on /register; starting a new quiz from here always drops any finished/stale state first.
  function goRegister() {
    reset()
    navigate('/register')
  }

  if (campaign.kind === 'loading') {
    return (
      <HomeShell>
        <StatusStage>
          <QuizEmblem className="size-[clamp(7rem,13vh,10rem)] max-sm:size-24" />
          <span className="spin mt-6 inline-block h-12 w-12 rounded-full border-4 border-white/20 border-t-[var(--p-gold-light)]" aria-hidden />
          <p className="mt-5 font-display text-[clamp(1.8rem,2.8vw,3rem)] font-semibold max-sm:text-[1.6rem]">Kampaniya yüklənir…</p>
        </StatusStage>
      </HomeShell>
    )
  }

  if (campaign.kind === 'error') {
    const copy = ERROR_COPY[campaign.error]
    return (
      <HomeShell>
        <StatusStage alert>
          <QuizEmblem className="size-[clamp(6rem,11vh,9rem)] max-sm:size-20" />
          <h1 className="mt-5 max-w-[24ch] font-display text-[clamp(2.4rem,4.4vw,4.8rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[2rem]">{copy.title}</h1>
          <p className="mt-4 max-w-[40rem] text-[clamp(1.1rem,1.6vw,1.7rem)] leading-relaxed text-[#d6deec] max-sm:text-base">{copy.text}</p>
        </StatusStage>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="home-actions">
          <button type="button" onClick={retry} className={`${PRIMARY} w-full max-w-[34rem]`} data-testid="home-primary">
            Yenidən yoxla
          </button>
        </div>
      </HomeShell>
    )
  }

  const { data } = campaign
  const { book } = data
  const author = meaningfulAuthor(book.author)
  // Presentation only (no campaign is special-cased): long titles get a smaller type so the stage,
  // rules and the start button still fit a kiosk screen without scrolling.
  const longTitle = book.title.trim().length > 24
  const titleSize = longTitle
    ? 'text-[clamp(2.6rem,min(4.2vw,7.2vh),5.2rem)] max-sm:text-[1.9rem]'
    : 'text-[clamp(3.4rem,min(6.4vw,11vh),8rem)] max-sm:text-[2.35rem]'
  const rewardSize = data.rewardTitle.trim().length > 36
    ? 'text-[clamp(1.35rem,1.8vw,2.1rem)] max-sm:text-[1.1rem]'
    : 'text-[clamp(1.6rem,2.3vw,2.7rem)] max-sm:text-[1.25rem]'
  const rules: [string, string][] = [
    ['Suallar', `${data.questionCount} sual`],
    ['Keçid balı', `${data.passingScore} / ${data.questionCount}`],
    ['Qayda', '1 iştirakçı — 1 cəhd'],
  ]

  return (
    <HomeShell>
      <section
        aria-labelledby="campaign-title"
        data-testid="home-stage"
        className="home-stage rise grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-[clamp(2rem,3.6vw,4.8rem)] rounded-[clamp(1.2rem,1.6vw,2rem)] px-[clamp(2rem,3.6vw,5rem)] py-[clamp(1.4rem,3.4vh,3.2rem)] [grid-template-areas:'art_title'_'art_body'] max-lg:gap-x-6 max-lg:gap-y-5 max-lg:px-8 max-lg:py-7 max-lg:[grid-template-areas:'art_title'_'body_body'] max-sm:gap-x-4 max-sm:gap-y-4 max-sm:rounded-2xl max-sm:px-4 max-sm:py-5"
      >
        <CampaignArt book={book} />

        <div className="min-w-0 self-end [grid-area:title] max-lg:self-center">
          <p lang="az" className="font-display text-[clamp(1.15rem,1.5vw,1.7rem)] font-semibold uppercase tracking-[0.16em] text-[var(--p-gold-light)] max-sm:text-[0.82rem] max-sm:tracking-[0.1em]">
            {formatRange(data.startDate, data.endDate)}
          </p>
          <h1
            id="campaign-title"
            lang="az"
            className={`mt-[clamp(0.25rem,0.8vh,0.7rem)] font-display font-bold leading-[0.95] text-[#fbf6ec] [overflow-wrap:anywhere] [text-wrap:balance] ${titleSize}`}
          >
            {book.title}
          </h1>
          {author && (
            <p className="mt-[clamp(0.4rem,1vh,0.8rem)] text-[clamp(1.15rem,1.7vw,1.9rem)] font-medium text-[#c9d3e6] max-sm:mt-1 max-sm:text-[0.95rem]">{author}</p>
          )}
        </div>

        <div className="min-w-0 self-start [grid-area:body]">
          <p
            lang="az"
            data-testid="campaign-description"
            className="mt-[clamp(0.7rem,1.8vh,1.5rem)] max-w-[64ch] text-[clamp(1.05rem,min(1.35vw,2.4vh),1.55rem)] leading-relaxed text-[#d6deec] [overflow-wrap:anywhere] lg:line-clamp-3 max-lg:mt-0 max-sm:text-[0.95rem] max-sm:leading-normal"
          >
            {book.description}
          </p>

          {data.rewardTitle && (
            <div data-testid="reward" className="mt-[clamp(0.9rem,2.4vh,2rem)] flex items-center gap-[clamp(0.8rem,1.2vw,1.3rem)] max-sm:mt-4 max-sm:gap-3">
              <RewardMedal className="h-[clamp(3.4rem,5vw,5.2rem)] w-auto shrink-0 max-sm:h-12" />
              <div className="min-w-0">
                <p className="text-[clamp(0.85rem,1vw,1.1rem)] font-semibold uppercase tracking-[0.14em] text-[var(--p-gold-light)] max-sm:text-[0.72rem]">Mükafat</p>
                <p lang="az" className={`font-display font-bold leading-tight text-[#fbf6ec] [overflow-wrap:anywhere] ${rewardSize}`}>{data.rewardTitle}</p>
              </div>
            </div>
          )}

          <dl
            data-testid="home-rules"
            className="mt-[clamp(1rem,2.6vh,2.2rem)] grid w-full max-w-[64rem] grid-cols-[minmax(0,0.8fr)_minmax(0,0.8fr)_minmax(0,1.4fr)] divide-x divide-[rgba(233,192,105,0.35)] overflow-hidden rounded-2xl bg-white/[0.07] ring-1 ring-[rgba(233,192,105,0.4)] max-sm:mt-4 max-sm:rounded-xl"
          >
            {rules.map(([label, value]) => (
              <div key={label} className="flex min-w-0 flex-col justify-center gap-1 px-[clamp(0.9rem,1.6vw,1.8rem)] py-[clamp(0.6rem,1.4vh,1.1rem)] max-sm:px-2 max-sm:py-2.5">
                <dt className="text-[clamp(0.8rem,0.95vw,1.05rem)] font-semibold uppercase tracking-[0.12em] text-[var(--p-gold-light)] max-sm:text-[0.66rem] max-sm:tracking-[0.06em]">{label}</dt>
                <dd lang="az" className="font-display text-[clamp(1.5rem,2.2vw,2.5rem)] font-bold leading-tight text-[#fbf6ec] lg:whitespace-nowrap max-sm:text-[1.02rem]">{value}</dd>
              </div>
            ))}
          </dl>
        </div>
      </section>

      <nav aria-label="Ana səhifə seçimləri" data-testid="home-actions" className="rise flex flex-col items-center gap-[clamp(0.5rem,1.2vh,0.9rem)] [animation-delay:90ms]">
        <div className="flex w-full max-w-[78rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.5rem)] max-sm:flex-col max-sm:gap-3">
          {activeGame ? (
            <button type="button" onClick={() => navigate('/game')} className={`${PRIMARY} flex-[1.7]`} data-testid="home-primary">
              DAVAM ET
              <PlayIcon className="size-[0.8em] shrink-0" />
            </button>
          ) : (
            <button type="button" onClick={goRegister} className={`${PRIMARY} flex-[1.7]`} data-testid="home-primary">
              QUİZƏ BAŞLA
              <PlayIcon className="size-[0.8em] shrink-0" />
            </button>
          )}
          <button type="button" onClick={() => navigate(`/leaderboard/${data.campaignId}`)} className={`${SECONDARY} flex-1`} data-testid="home-leaderboard">
            LİDER CƏDVƏLİ
          </button>
        </div>

        {activeGame ? (
          <div className="flex flex-wrap items-center justify-center gap-x-5 gap-y-1 text-center max-sm:flex-col max-sm:gap-y-2">
            <p className="text-[clamp(1rem,1.25vw,1.35rem)] text-[var(--p-ink-2)] max-sm:text-[0.95rem]">
              Başlanmış quiz var: sual {activeGame.questionNumber} / {activeGame.totalQuestions}. Davam etsəniz, vaxt sıfırlanmır.
            </p>
            <button
              type="button"
              onClick={goRegister}
              data-testid="home-new-participant"
              className="tap min-h-12 rounded-full px-4 text-[clamp(1rem,1.25vw,1.35rem)] font-semibold text-[var(--p-burgundy)] underline decoration-[var(--p-gold)] decoration-2 underline-offset-[6px] max-sm:min-h-[3.25rem]"
            >
              Növbəti iştirakçı üçün başlat
            </button>
          </div>
        ) : (
          <p className="text-[clamp(0.85rem,1vw,1.05rem)] text-[var(--p-ink-2)] max-sm:hidden">Ekrana toxunaraq oynayın</p>
        )}
      </nav>
    </HomeShell>
  )
}
