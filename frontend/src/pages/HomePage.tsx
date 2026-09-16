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

type CategoriesLoad =
  | { kind: 'loading' }
  | { kind: 'ready'; data: CampaignSummary[] }
  | { kind: 'empty' }
  | { kind: 'error'; error: CampaignErrorKind }

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

/** Hero banner: the "Bilik Bağı" presentation name and the category-selection instruction. Category data never appears here. */
function Hero() {
  return (
    <header className="home-stage rise flex flex-col items-center gap-[clamp(0.15rem,0.5vh,0.4rem)] short:gap-1 rounded-[clamp(1rem,1.4vw,1.6rem)] px-[clamp(1.5rem,3.4vw,3.5rem)] py-[clamp(0.9rem,2.2vh,1.6rem)] short:py-3 text-center max-sm:rounded-2xl max-sm:px-5 max-sm:py-4">
      <p lang="az" className="font-display text-[clamp(0.9rem,1.15vw,1.2rem)] font-semibold uppercase tracking-[0.22em] text-[var(--p-gold-light)] max-sm:text-[0.72rem] max-sm:tracking-[0.14em]">
        Oxu • Tanı • Cavablandır
      </p>
      <h1 lang="az" className="font-display text-[clamp(2rem,4vw,3.6rem)] short:text-[clamp(1.7rem,3.2vw,2.6rem)] font-bold uppercase leading-[0.95] tracking-[0.03em] text-[#fbf6ec] max-sm:text-[1.7rem]">
        Bilik Bağı
      </h1>
      <p lang="az" className="mt-[clamp(0.1rem,0.4vh,0.35rem)] text-[clamp(0.95rem,1.15vw,1.25rem)] font-medium text-[#d6deec] max-sm:text-[0.92rem]">
        Kateqoriyanı seçin
      </p>
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
    return (
      <GameShowShell>
        <section
          aria-labelledby="resume-title"
          data-testid="home-stage"
          className="home-stage rise flex min-h-[clamp(20rem,52vh,36rem)] flex-col items-center justify-center rounded-[clamp(1.2rem,1.6vw,2rem)] px-[clamp(1.5rem,4vw,5rem)] py-[clamp(2rem,5vh,4rem)] text-center max-sm:min-h-[22rem] max-sm:rounded-2xl max-sm:px-5 max-sm:py-8"
        >
          <QuizEmblem className="size-[clamp(6rem,11vh,9rem)] max-sm:size-20" />
          <p className="mt-4 font-display text-[clamp(0.9rem,1.1vw,1.15rem)] font-semibold uppercase tracking-[0.18em] text-[var(--p-gold-light)] max-sm:text-[0.75rem]">
            Davam edən sessiya
          </p>
          <h1 id="resume-title" lang="az" className="mt-1 max-w-[26ch] font-display text-[clamp(2.2rem,3.8vw,4.2rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[1.9rem]">
            {activeGame.quizMode.title}
          </h1>
          <p className="mt-4 max-w-[42rem] text-[clamp(1.05rem,1.5vw,1.6rem)] leading-relaxed text-[#d6deec] max-sm:text-base">
            Başlanmış quiz var: sual {activeGame.questionNumber} / {activeGame.totalQuestions}. Davam etsəniz, vaxt sıfırlanmır.
          </p>
        </section>
        <nav aria-label="Sessiya seçimləri" data-testid="home-actions" className="rise flex w-full max-w-[78rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.5rem)] self-center [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
          <button type="button" onClick={() => navigate('/game')} className={`${PRIMARY} flex-[1.7]`} data-testid="home-primary">
            DAVAM ET
            <PlayIcon className="size-[0.8em] shrink-0" />
          </button>
          <button type="button" onClick={cancelSession} className={`${SECONDARY} flex-1`} data-testid="home-cancel-session">
            SESSİYANI LƏĞV ET
          </button>
        </nav>
      </GameShowShell>
    )
  }

  if (categories.kind === 'loading') {
    return (
      <GameShowShell>
        <StatusStage>
          <QuizEmblem className="size-[clamp(7rem,13vh,10rem)] max-sm:size-24" />
          <span className="spin mt-6 inline-block h-12 w-12 rounded-full border-4 border-white/20 border-t-[var(--p-gold-light)]" aria-hidden />
          <p className="mt-5 font-display text-[clamp(1.8rem,2.8vw,3rem)] font-semibold max-sm:text-[1.6rem]">Kateqoriyalar yüklənir…</p>
        </StatusStage>
      </GameShowShell>
    )
  }

  if (categories.kind === 'error') {
    const copy = ERROR_COPY[categories.error]
    return (
      <GameShowShell>
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
      </GameShowShell>
    )
  }

  if (categories.kind === 'empty') {
    return (
      <GameShowShell>
        <StatusStage>
          <QuizEmblem className="size-[clamp(6rem,11vh,9rem)] max-sm:size-20" />
          <h1 className="mt-5 max-w-[24ch] font-display text-[clamp(2rem,3.6vw,3.8rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[1.7rem]">Hazırda heç bir kateqoriya yoxdur</h1>
          <p className="mt-4 max-w-[40rem] text-[clamp(1.05rem,1.4vw,1.5rem)] leading-relaxed text-[#d6deec] max-sm:text-base">Yeni bilik yarışları tezliklə əlavə olunacaq.</p>
        </StatusStage>
        <div className="rise flex justify-center [animation-delay:90ms]" data-testid="home-actions">
          <button type="button" onClick={retry} className={`${SECONDARY} w-full max-w-[34rem]`} data-testid="home-primary">
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
      <Hero />
      <ul
        data-testid="category-grid"
        className="grid w-full grid-cols-2 gap-[clamp(0.7rem,1.3vw,1.3rem)] short:gap-[0.55rem] max-sm:grid-cols-1 max-sm:gap-3"
      >
        {data.map((campaign, index) => (
          <li
            key={campaign.campaignId}
            className={`min-w-0 ${
              isLastOdd && index === data.length - 1 ? 'col-span-2 mx-auto w-1/2 max-sm:col-span-1 max-sm:mx-0 max-sm:w-full' : ''
            }`}
          >
            <CategoryCard
              campaign={campaign}
              onSelect={() => selectCategory(campaign.campaignId)}
              onLeaderboard={() => navigate(`/leaderboard/${campaign.campaignId}`)}
            />
          </li>
        ))}
      </ul>
    </GameShowShell>
  )
}
