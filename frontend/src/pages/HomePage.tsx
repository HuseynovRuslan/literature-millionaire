import { useEffect, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { classifyCampaignError, getCurrentCampaign, type CampaignErrorKind } from '../api/campaigns'
import { KioskHeader } from '../components/national/KioskBrand'
import CarpetFrame from '../components/national/CarpetFrame'
import { Buta, ButaRule } from '../components/national/Ornaments'
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
  'no-active': { title: 'Hazırda aktiv kampaniya yoxdur', text: '"Ayın kitabı" viktorinası tezliklə yenidən başlayacaq.' },
  multiple: { title: 'Kampaniya tənzimlənməsində xəta var', text: 'Eyni vaxtda bir neçə kampaniya aktivdir. Zəhmət olmasa, inzibatçıya müraciət edin.' },
  unavailable: { title: 'Serverlə əlaqə yoxdur', text: 'Şəbəkə bağlantısını yoxlayın və yenidən cəhd edin.' },
  unexpected: { title: 'Gözlənilməz xəta baş verdi', text: 'Bir az sonra yenidən cəhd edin.' },
}

type CampaignLoad =
  | { kind: 'loading' }
  | { kind: 'ready'; data: CurrentCampaign }
  | { kind: 'error'; error: CampaignErrorKind }

const CTA =
  'tap flex min-h-[clamp(4.5rem,10vh,7rem)] w-full items-center justify-center gap-4 rounded-full px-10 font-display text-[clamp(1.8rem,3vw,3rem)] font-bold tracking-[0.06em]'

/** Gold-framed cover with buta corners. Real image when it loads; otherwise a designed paper placeholder. */
function BookCover({ book }: { book: CampaignBook }) {
  const [status, setStatus] = useState<'pending' | 'ok' | 'failed'>(book.coverImageUrl ? 'pending' : 'failed')
  const showImage = status === 'ok'
  return (
    <div className="relative mx-auto w-[clamp(13rem,21vw,25rem)] max-w-full shrink-0" data-testid="cover-frame">
      <div className="relative aspect-[2/3] rounded-md border-[6px] border-[var(--p-gold)] bg-[var(--p-paper-2)] p-1.5 shadow-[var(--p-shadow)]">
        <div className="relative h-full w-full overflow-hidden rounded-sm border border-[var(--p-gold-light)]">
          {status !== 'failed' && (
            <img
              src={book.coverImageUrl}
              alt={`"${book.title}" kitabının üz qabığı`}
              onLoad={() => setStatus('ok')}
              onError={() => setStatus('failed')}
              className={`absolute inset-0 h-full w-full object-cover ${showImage ? '' : 'opacity-0'}`}
            />
          )}
          {!showImage && (
            <div
              role="img"
              aria-label={`"${book.title}", ${book.author} — üz qabığı əvəzedicisi`}
              data-testid="cover-fallback"
              className="flex h-full flex-col items-center justify-center bg-[var(--p-indigo)] px-[10%] text-center"
            >
              <span aria-hidden className="mb-5 h-px w-14 bg-[var(--p-gold-light)]" />
              <span lang="az" className="font-display text-[clamp(1.8rem,2.8vw,3.2rem)] font-bold leading-[1.05] text-[var(--p-paper)] [overflow-wrap:anywhere]">
                {book.title}
              </span>
              <span aria-hidden className="my-5 h-px w-14 bg-[var(--p-gold-light)]" />
              <span className="text-[clamp(0.9rem,1.15vw,1.25rem)] font-medium leading-snug text-[var(--p-gold-light)]">{book.author}</span>
            </div>
          )}
        </div>
      </div>
      <Buta className="absolute -left-4 -top-5 h-12 w-9 drop-shadow" flip />
      <Buta className="absolute -right-4 -top-5 h-12 w-9 drop-shadow" />
      <Buta className="absolute -bottom-5 -left-4 h-12 w-9 rotate-180 drop-shadow" />
      <Buta className="absolute -bottom-5 -right-4 h-12 w-9 rotate-180 drop-shadow" flip />
    </div>
  )
}

/** Shared shell for every home state: paper background, carpet border, product/brand header, footer hint. */
function PaperShell({ children, alert = false }: { children: ReactNode; alert?: boolean }) {
  return (
    <main className="kiosk paper flex flex-col">
      <CarpetFrame />
      <KioskHeader />
      <div
        role={alert ? 'alert' : undefined}
        className="relative z-0 flex min-h-0 flex-1 flex-col items-center justify-center"
        style={{ padding: '0.4rem calc(var(--frame) + 2rem) calc(var(--frame) + 2.4rem)' }}
      >
        {children}
      </div>
      <p
        className="absolute left-1/2 -translate-x-1/2 text-[clamp(0.8rem,1vw,1rem)] text-[var(--p-ink-2)]"
        style={{ bottom: 'calc(var(--frame) + 0.7rem)' }}
      >
        Ekrana toxunaraq oynayın
      </p>
    </main>
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
      <PaperShell>
        <div role="status" aria-live="polite" className="flex flex-col items-center gap-6">
          <span className="spin inline-block h-14 w-14 rounded-full border-4 border-[var(--p-gold-light)] border-t-[var(--p-burgundy)]" aria-hidden />
          <p className="font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-semibold text-[var(--p-burgundy)]">Kampaniya yüklənir…</p>
        </div>
      </PaperShell>
    )
  }

  if (campaign.kind === 'error') {
    const copy = ERROR_COPY[campaign.error]
    return (
      <PaperShell alert>
        <div className="flex w-full max-w-[56rem] flex-col items-center text-center">
          <p className="font-display text-[clamp(1.4rem,2.2vw,2.2rem)] font-semibold tracking-[0.18em] text-[var(--p-burgundy)]">AYIN KİTABI</p>
          <h1 className="mt-4 font-display text-[clamp(2.4rem,5vw,5.2rem)] font-bold leading-tight text-[var(--p-ink)]">{copy.title}</h1>
          <ButaRule className="my-8 w-full max-w-[30rem]" />
          <p className="max-w-[36rem] text-[clamp(1.1rem,1.6vw,1.7rem)] leading-relaxed text-[var(--p-ink-2)]">{copy.text}</p>
          <button type="button" onClick={retry} className={`${CTA} paper-cta mt-12 max-w-[30rem]`}>
            Yenidən yoxla
          </button>
        </div>
      </PaperShell>
    )
  }

  const { data } = campaign
  const { book } = data

  return (
    <PaperShell>
      <section className="grid w-full max-w-[100rem] grid-cols-1 items-center gap-8 lg:grid-cols-[auto_minmax(0,1fr)] lg:gap-14">
        <BookCover book={book} />

        <div className="flex min-w-0 flex-col text-center lg:text-left">
          <div className="flex items-center justify-center gap-3 lg:justify-start">
            <Buta className="h-7 w-5" flip />
            <p className="font-display text-[clamp(1.3rem,2vw,2rem)] font-semibold tracking-[0.18em] text-[var(--p-burgundy)]">AYIN KİTABI</p>
            <Buta className="h-7 w-5" />
          </div>
          <h1 lang="az" className="mt-2 font-display text-[clamp(3rem,6.2vw,6.8rem)] font-bold leading-[0.98] text-[var(--p-burgundy)] [overflow-wrap:anywhere]">
            {book.title}
          </h1>
          <p className="mt-3 text-[clamp(1.2rem,1.9vw,2rem)] font-medium text-[var(--p-indigo)] short:mt-2">{book.author}</p>

          <p lang="az" className="mt-5 line-clamp-4 max-w-[60ch] text-[clamp(1.05rem,1.4vw,1.45rem)] leading-relaxed text-[var(--p-ink)] [overflow-wrap:anywhere] lg:line-clamp-5 short:mt-3 short:line-clamp-3">
            {book.description}
          </p>

          <ButaRule className="my-6 w-full max-w-[40rem] self-center lg:self-start short:my-4" />

          <dl className="flex flex-wrap justify-center gap-x-10 gap-y-3 text-[clamp(1rem,1.35vw,1.4rem)] lg:justify-start">
            <div><dt className="text-[var(--p-ink-2)]">Kampaniya</dt><dd className="font-semibold text-[var(--p-ink)]">{formatRange(data.startDate, data.endDate)}</dd></div>
            <div><dt className="text-[var(--p-ink-2)]">Suallar</dt><dd className="font-semibold text-[var(--p-ink)]">{data.questionCount} sual</dd></div>
            <div><dt className="text-[var(--p-ink-2)]">Keçid balı</dt><dd className="font-semibold text-[var(--p-ink)]">{data.passingScore}/{data.questionCount}</dd></div>
            {data.rewardTitle && (
              <div><dt className="text-[var(--p-ink-2)]">Mükafat</dt><dd className="font-semibold text-[var(--p-burgundy)]">{data.rewardTitle}</dd></div>
            )}
          </dl>

          <div className="mt-8 flex w-full max-w-[44rem] flex-col gap-4 self-center lg:self-start short:mt-5 short:gap-3">
            {activeGame ? (
              <>
                <button type="button" onClick={() => navigate('/game')} className={`${CTA} paper-cta`}>
                  Davam et
                </button>
                <p className="text-center text-[clamp(0.95rem,1.2vw,1.25rem)] text-[var(--p-ink-2)] lg:text-left">
                  Başlanmış quiz var: sual {activeGame.questionNumber} / {activeGame.totalQuestions}. Davam etsəniz, vaxt sıfırlanmır.
                </p>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <button type="button" onClick={() => navigate(`/leaderboard/${data.campaignId}`)} className="tap paper-ghost min-h-[4.5rem] rounded-full px-6 text-[clamp(1rem,1.3vw,1.3rem)] font-semibold">
                    LİDER CƏDVƏLİ
                  </button>
                  <button type="button" onClick={goRegister} className="tap paper-ghost min-h-[4.5rem] rounded-full px-6 text-[clamp(0.9rem,1.15vw,1.15rem)] font-medium">
                    Əvvəlkini ləğv et və yeni quiz başlat
                  </button>
                </div>
              </>
            ) : (
              <>
                <button type="button" onClick={goRegister} className={`${CTA} paper-cta`}>
                  QUİZƏ BAŞLA
                </button>
                <button type="button" onClick={() => navigate(`/leaderboard/${data.campaignId}`)} className="tap paper-ghost min-h-[4.5rem] rounded-full px-8 font-display text-[clamp(1.2rem,1.8vw,1.8rem)] font-semibold tracking-[0.04em]">
                  LİDER CƏDVƏLİ
                </button>
              </>
            )}
          </div>
        </div>
      </section>
    </PaperShell>
  )
}
