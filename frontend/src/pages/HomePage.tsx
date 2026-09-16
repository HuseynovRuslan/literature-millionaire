import { useEffect, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { classifyCampaignError, getAvailableCampaigns, type CampaignErrorKind } from '../api/campaigns'
import CategoryCard from '../components/home/CategoryCard'
import { PlayIcon, QuizEmblem } from '../components/home/GameShowArt'
import { PRIMARY_CTA as PRIMARY, SECONDARY_CTA as SECONDARY } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import { useGame } from '../game/GameContext'
import type { CampaignSummary } from '../types/campaign'

const ERROR_COPY: Record<CampaignErrorKind, { title: string; text: string }> = {
  'no-active': { title: 'Hazırda aktiv bilik yarışı yoxdur', text: 'Yeni kateqoriyalar tezliklə əlavə olunacaq.' },
  multiple: { title: 'Kateqoriya tənzimlənməsində xəta var', text: 'Eyni vaxtda bir neçə kampaniya aktivdir. Zəhmət olmasa, inzibatçıya müraciət edin.' },
  unavailable: { title: 'Serverlə əlaqə yoxdur', text: 'Şəbəkə bağlantısını yoxlayın və yenidən cəhd edin.' },
  unexpected: { title: 'Gözlənilməz xəta baş verdi', text: 'Bir az sonra yenidən cəhd edin.' },
}

const STEPS = [
  { title: 'Kateqoriya seç', text: 'Sevdiyin mövzunu götür' },
  { title: 'Qeydiyyatdan keç', text: 'Ad və telefon nömrəsi' },
  { title: 'Cavabla və qazan', text: 'Vaxt azdır, tələs!' },
]

type CategoriesLoad =
  | { kind: 'loading' }
  | { kind: 'ready'; data: CampaignSummary[] }
  | { kind: 'empty' }
  | { kind: 'error'; error: CampaignErrorKind }

/** Loading, empty, error and resume states share one centred card. */
function StatusStage({ children, alert = false, labelledBy }: { children: ReactNode; alert?: boolean; labelledBy?: string }) {
  return (
    <section
      role={labelledBy ? undefined : alert ? 'alert' : 'status'}
      aria-live={labelledBy || alert ? undefined : 'polite'}
      aria-labelledby={labelledBy}
      data-testid="home-stage"
      className="card card-glow rise mx-auto flex min-h-[24rem] w-full max-w-[56rem] flex-col items-center justify-center rounded-[2rem] px-[clamp(1.5rem,4vw,3.5rem)] py-12 text-center max-sm:min-h-[20rem] max-sm:rounded-3xl max-sm:px-5 max-sm:py-9"
    >
      {children}
    </section>
  )
}

/** Hero: the "Bilik Bağı" show title, the instruction and a three-step how-to-play strip. No category data here. */
function Hero({ count }: { count: number }) {
  return (
    <header className="rise grid grid-cols-[minmax(0,1fr)_auto] items-center gap-[clamp(1.5rem,4vw,4rem)] max-lg:grid-cols-1">
      <div className="min-w-0">
        <p lang="az" className="chip px-4 py-2 text-[clamp(0.75rem,0.9vw,0.9rem)] uppercase tracking-[0.16em] text-brand-soft max-sm:text-[0.7rem]">
          Oxu • Tanı • Cavablandır
        </p>
        <h1 lang="az" className="text-gradient mt-4 font-display text-[clamp(2.6rem,6vw,5.6rem)] font-extrabold uppercase leading-[0.95] tracking-tight max-sm:text-[2.4rem]">
          Bilik Bağı
        </h1>
        <p lang="az" className="mt-3 text-[clamp(1.05rem,1.4vw,1.35rem)] font-semibold text-fg-2 max-sm:text-base">
          Kateqoriyanı seçin{count > 0 ? ` — ${count} yarış sizi gözləyir` : ''}
        </p>

        <ol className="mt-6 grid max-w-[52rem] grid-cols-3 gap-3 max-sm:mt-4 max-sm:gap-2">
          {STEPS.map((step, i) => (
            <li key={step.title} className="flex items-center gap-3 rounded-2xl bg-white/[0.05] px-3.5 py-3 ring-1 ring-white/10 max-sm:flex-col max-sm:gap-1.5 max-sm:px-2 max-sm:py-2.5 max-sm:text-center">
              <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-brand font-display text-base font-extrabold text-white shadow-[0_3px_0_#4a33c9] max-sm:size-8 max-sm:text-sm">
                {i + 1}
              </span>
              <span className="min-w-0">
                <span lang="az" className="block text-[clamp(0.9rem,1vw,1rem)] font-extrabold leading-tight text-fg max-sm:text-[0.78rem]">{step.title}</span>
                <span lang="az" className="block text-[clamp(0.8rem,0.88vw,0.88rem)] font-medium text-fg-3 max-sm:hidden">{step.text}</span>
              </span>
            </li>
          ))}
        </ol>
      </div>
      <QuizEmblem className="pop size-[clamp(9rem,15vw,15rem)] max-lg:hidden" />
    </header>
  )
}

export default function HomePage() {
  const navigate = useNavigate()
  const { state, reset } = useGame()
  // A valid, unanswered question restored from sessionStorage, with its category: offer to
  // continue rather than showing the category grid, so a stray tap can never start a second session.
  const activeGame =
    state.status === 'playing' && state.question && state.campaignId && state.quizMode
      ? { questionNumber: state.questionNumber, totalQuestions: state.totalQuestions, quizMode: state.quizMode }
      : null

  const [categories, setCategories] = useState<CategoriesLoad>({ kind: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (activeGame) return // no need to load categories while a session is being resumed or cancelled
    const controller = new AbortController()
    setCategories({ kind: 'loading' })
    getAvailableCampaigns(controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return
        setCategories(data.length === 0 ? { kind: 'empty' } : { kind: 'ready', data })
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return
        setCategories({ kind: 'error', error: classifyCampaignError(err) })
      })
    return () => controller.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attempt, !!activeGame])

  function retry() {
    setAttempt((a) => a + 1)
  }

  function selectCategory(campaignId: number) {
    reset() // no active session at this point, but always start a fresh registration slate
    navigate(`/register/${campaignId}`)
  }

  function cancelSession() {
    reset()
  }

  if (activeGame) {
    const progress = Math.round(((activeGame.questionNumber - 1) / activeGame.totalQuestions) * 100)
    return (
      <GameShowShell>
        <StatusStage labelledBy="resume-title">
          <QuizEmblem className="pop size-28 max-sm:size-24" />
          <p className="chip mt-5 px-4 py-1.5 text-[clamp(0.75rem,0.9vw,0.88rem)] uppercase tracking-[0.14em] text-sun">Davam edən sessiya</p>
          <h1 id="resume-title" lang="az" className="mt-3 max-w-[24ch] font-display text-[clamp(1.9rem,3.4vw,3rem)] font-bold leading-tight max-sm:text-[1.6rem]">
            {activeGame.quizMode.title}
          </h1>
          <p className="mt-3 max-w-[40rem] text-[clamp(1.05rem,1.4vw,1.3rem)] leading-relaxed text-fg-2 max-sm:text-base">
            Başlanmış quiz var: sual {activeGame.questionNumber} / {activeGame.totalQuestions}. Davam etsəniz, vaxt sıfırlanmır.
          </p>
          <div className="mt-5 h-3 w-full max-w-[26rem] overflow-hidden rounded-full bg-white/10" aria-hidden="true">
            <div className="h-full rounded-full bg-[linear-gradient(90deg,var(--color-brand),var(--color-sun))]" style={{ width: `${Math.max(progress, 6)}%` }} />
          </div>
        </StatusStage>
        <nav aria-label="Sessiya seçimləri" data-testid="home-actions" className="rise mx-auto flex w-full max-w-[56rem] items-stretch gap-4 [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
          <button type="button" onClick={() => navigate('/game')} className={`${PRIMARY} flex-[1.7]`} data-testid="home-primary">
            Davam et
            <PlayIcon className="size-[0.85em] shrink-0" />
          </button>
          <button type="button" onClick={cancelSession} className={`${SECONDARY} flex-1`} data-testid="home-cancel-session">
            Sessiyanı ləğv et
          </button>
        </nav>
      </GameShowShell>
    )
  }

  if (categories.kind === 'loading') {
    return (
      <GameShowShell>
        <StatusStage>
          <QuizEmblem className="pop size-32 max-sm:size-24" />
          <span className="spin mt-7 inline-block size-12 rounded-full border-4 border-white/15 border-t-sun" aria-hidden />
          <p className="mt-5 font-display text-[clamp(1.4rem,2.2vw,2rem)] font-bold max-sm:text-[1.3rem]">Kateqoriyalar yüklənir…</p>
        </StatusStage>
      </GameShowShell>
    )
  }

  if (categories.kind === 'error') {
    const copy = ERROR_COPY[categories.error]
    return (
      <GameShowShell>
        <StatusStage alert>
          <QuizEmblem className="size-28 max-sm:size-24" />
          <h1 className="mt-6 max-w-[24ch] font-display text-[clamp(1.9rem,3.6vw,3.2rem)] font-bold leading-tight max-sm:text-[1.6rem]">{copy.title}</h1>
          <p className="mt-3 max-w-[40rem] text-[clamp(1.05rem,1.4vw,1.3rem)] leading-relaxed text-fg-2 max-sm:text-base">{copy.text}</p>
        </StatusStage>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="home-actions">
          <button type="button" onClick={retry} className={`${PRIMARY} w-full max-w-[30rem]`} data-testid="home-primary">
            Yenidən yoxla
          </button>
        </div>
      </GameShowShell>
    )
  }

  if (categories.kind === 'empty') {
    return (
      <GameShowShell>
        <StatusStage>
          <QuizEmblem className="size-28 max-sm:size-24" />
          <h1 className="mt-6 max-w-[24ch] font-display text-[clamp(1.8rem,3.2vw,2.8rem)] font-bold leading-tight max-sm:text-[1.5rem]">Hazırda heç bir kateqoriya yoxdur</h1>
          <p className="mt-3 max-w-[40rem] text-[clamp(1.05rem,1.4vw,1.3rem)] leading-relaxed text-fg-2 max-sm:text-base">Yeni bilik yarışları tezliklə əlavə olunacaq.</p>
        </StatusStage>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="home-actions">
          <button type="button" onClick={retry} className={`${SECONDARY} w-full max-w-[30rem]`} data-testid="home-primary">
            Yenidən yoxla
          </button>
        </div>
      </GameShowShell>
    )
  }

  const { data } = categories
  const isLastOdd = data.length % 2 === 1

  return (
    <GameShowShell>
      <Hero count={data.length} />
      <ul
        data-testid="category-grid"
        className="grid w-full grid-cols-2 gap-[clamp(1rem,1.6vw,1.5rem)] xl:grid-cols-3 max-sm:grid-cols-1 max-sm:gap-4"
      >
        {data.map((campaign, index) => (
          <li
            key={campaign.campaignId}
            className={`min-w-0 ${
              isLastOdd && index === data.length - 1
                ? 'max-xl:col-span-2 max-xl:mx-auto max-xl:w-1/2 max-sm:col-span-1 max-sm:mx-0 max-sm:w-full'
                : ''
            }`}
          >
            <CategoryCard
              campaign={campaign}
              index={index}
              onSelect={() => selectCategory(campaign.campaignId)}
              onLeaderboard={() => navigate(`/leaderboard/${campaign.campaignId}`)}
            />
          </li>
        ))}
      </ul>
    </GameShowShell>
  )
}
