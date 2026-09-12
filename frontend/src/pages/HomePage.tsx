import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import GoldRule from '../components/GoldRule'
import { classifyCampaignError, getCurrentCampaign, type CampaignErrorKind } from '../api/campaigns'
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

/** Real cover when the file loads; otherwise a designed placeholder with title and author (never a broken-image icon). */
function BookCover({ book }: { book: CampaignBook }) {
  const [status, setStatus] = useState<'pending' | 'ok' | 'failed'>(book.coverImageUrl ? 'pending' : 'failed')
  const showImage = status === 'ok'
  return (
    <div className="relative mx-auto aspect-[2/3] w-[clamp(14rem,22vw,26rem)] max-w-full shrink-0">
      {status !== 'failed' && (
        <img
          src={book.coverImageUrl}
          alt={`"${book.title}" kitabının üz qabığı`}
          onLoad={() => setStatus('ok')}
          onError={() => setStatus('failed')}
          className={`absolute inset-0 h-full w-full rounded-lg object-cover shadow-[0_30px_60px_-20px_rgba(0,0,0,0.8)] ${showImage ? '' : 'opacity-0'}`}
        />
      )}
      {!showImage && (
        <div
          role="img"
          aria-label={`"${book.title}", ${book.author} — üz qabığı əvəzedicisi`}
          data-testid="cover-fallback"
          className="absolute inset-0 flex flex-col overflow-hidden rounded-lg bg-gradient-to-br from-navy-700 via-navy-800 to-navy-900 shadow-[0_30px_60px_-20px_rgba(0,0,0,0.8)]"
        >
          <span aria-hidden className="absolute inset-y-0 left-0 w-[7%] bg-gradient-to-r from-black/45 to-transparent" />
          <span aria-hidden className="absolute inset-3 rounded-md border border-gold/50" />
          <span aria-hidden className="absolute inset-5 rounded-sm border border-gold/25" />
          <div className="relative flex h-full flex-col items-center justify-center px-[12%] text-center">
            <span aria-hidden className="mb-6 h-px w-16 bg-gold/70" />
            <span lang="az" className="font-display text-[clamp(1.9rem,3vw,3.4rem)] font-bold leading-[1.05] text-ivory [overflow-wrap:anywhere]">
              {book.title}
            </span>
            <span aria-hidden className="my-6 h-px w-16 bg-gold/70" />
            <span className="text-[clamp(0.95rem,1.2vw,1.35rem)] font-medium leading-snug text-gold-light">{book.author}</span>
          </div>
        </div>
      )}
    </div>
  )
}

const CTA =
  'tap flex min-h-[7rem] w-full items-center justify-center gap-4 rounded-2xl px-10 font-display text-[clamp(1.8rem,3vw,3rem)] font-bold tracking-[0.05em]'

export default function HomePage() {
  const navigate = useNavigate()
  const { state, startGame, error: startError } = useGame()
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

  const starting = state.status === 'starting'
  // A valid, unanswered question restored from sessionStorage: offer to continue instead of starting a second session.
  const activeGame = state.status === 'playing' && state.question ? state : null

  async function handleStart() {
    if (starting) return
    const ok = await startGame() // GameContext ignores overlapping calls, so a double tap yields one request
    if (ok) navigate('/game')
  }

  if (campaign.kind === 'loading') {
    return (
      <main className="kiosk ornament flex flex-col items-center justify-center px-6 text-center">
        <div role="status" aria-live="polite" className="rise flex flex-col items-center gap-6">
          <span className="spin inline-block h-14 w-14 rounded-full border-4 border-gold/30 border-t-gold" aria-hidden />
          <p className="font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-semibold text-gold-light">Kampaniya yüklənir…</p>
        </div>
      </main>
    )
  }

  if (campaign.kind === 'error') {
    const copy = ERROR_COPY[campaign.error]
    return (
      <main className="kiosk ornament flex flex-col items-center justify-center px-6 text-center">
        <div role="alert" className="rise flex w-full max-w-[56rem] flex-col items-center">
          <p className="font-display text-[clamp(1.4rem,2.2vw,2.2rem)] font-semibold tracking-[0.12em] text-gold-light">AYIN KİTABI</p>
          <h1 className="mt-4 font-display text-[clamp(2.4rem,5vw,5.2rem)] font-bold leading-tight text-ivory">{copy.title}</h1>
          <GoldRule className="my-8 w-full max-w-[30rem]" />
          <p className="max-w-[36rem] text-[clamp(1.1rem,1.6vw,1.7rem)] leading-relaxed text-mist">{copy.text}</p>
          <button type="button" onClick={retry} className={`${CTA} mt-12 max-w-[30rem] bg-gold text-navy-900 shadow-[0_18px_50px_-12px_rgba(212,168,59,0.55)]`}>
            Yenidən yoxla
          </button>
        </div>
      </main>
    )
  }

  const { data } = campaign
  const { book } = data

  return (
    <main className="kiosk ornament flex flex-col items-center justify-center px-8 py-8 lg:px-16">
      <section className="rise grid w-full max-w-[100rem] grid-cols-1 items-center gap-8 lg:grid-cols-[auto_minmax(0,1fr)] lg:gap-16">
        <BookCover book={book} />

        <div className="flex min-w-0 flex-col text-center lg:text-left">
          <p className="font-display text-[clamp(1.3rem,2vw,2rem)] font-semibold tracking-[0.14em] text-gold-light">AYIN KİTABI</p>
          <h1 lang="az" className="mt-2 font-display text-[clamp(3rem,6.4vw,7rem)] font-bold leading-[0.98] text-ivory [overflow-wrap:anywhere]">
            {book.title}
          </h1>
          <p className="mt-3 text-[clamp(1.2rem,1.9vw,2rem)] font-medium text-mist">{book.author}</p>

          <p lang="az" className="mt-6 line-clamp-5 max-w-[60ch] text-[clamp(1.05rem,1.45vw,1.5rem)] leading-relaxed text-ivory/85 [overflow-wrap:anywhere]">
            {book.description}
          </p>

          <GoldRule className="my-7 w-full max-w-[40rem] self-center lg:self-start" />

          <dl className="flex flex-wrap justify-center gap-x-10 gap-y-3 text-[clamp(1rem,1.35vw,1.4rem)] lg:justify-start">
            <div><dt className="text-mist/80">Kampaniya</dt><dd className="font-semibold text-ivory">{formatRange(data.startDate, data.endDate)}</dd></div>
            <div><dt className="text-mist/80">Suallar</dt><dd className="font-semibold text-ivory">{data.questionCount} sual</dd></div>
            <div><dt className="text-mist/80">Keçid balı</dt><dd className="font-semibold text-ivory">{data.passingScore}/{data.questionCount}</dd></div>
            {data.rewardTitle && (
              <div><dt className="text-mist/80">Mükafat</dt><dd className="font-semibold text-gold-light">{data.rewardTitle}</dd></div>
            )}
          </dl>

          <div className="mt-9 flex w-full max-w-[44rem] flex-col gap-4 self-center lg:self-start">
            {activeGame ? (
              <>
                <button type="button" onClick={() => navigate('/game')} className={`${CTA} bg-gold text-navy-900 shadow-[0_18px_50px_-12px_rgba(212,168,59,0.55)]`}>
                  Davam et
                </button>
                <p className="text-center text-[clamp(0.95rem,1.2vw,1.25rem)] text-mist lg:text-left">
                  Başlanmış quiz var: sual {activeGame.questionNumber} / {activeGame.totalQuestions}. Davam etsəniz, vaxt sıfırlanmır.
                </p>
                <button type="button" onClick={handleStart} disabled={starting} aria-busy={starting} className="tap min-h-[4rem] rounded-xl border-2 border-gold/60 px-8 text-[clamp(1rem,1.3vw,1.3rem)] font-medium text-gold-light disabled:opacity-60">
                  {starting ? 'Quiz hazırlanır…' : 'Əvvəlkini ləğv et və yeni quiz başlat'}
                </button>
              </>
            ) : (
              <button type="button" onClick={handleStart} disabled={starting} aria-busy={starting} className={`${CTA} bg-gold text-navy-900 shadow-[0_18px_50px_-12px_rgba(212,168,59,0.55)] disabled:bg-gold/70`}>
                {starting ? (
                  <>
                    <span className="spin inline-block h-9 w-9 rounded-full border-4 border-navy-900/30 border-t-navy-900" aria-hidden />
                    Quiz hazırlanır
                  </>
                ) : (
                  'QUİZƏ BAŞLA'
                )}
              </button>
            )}

            {startError && (
              <div role="alert" className="flex flex-col items-center gap-3 rounded-2xl border border-bad/50 bg-bad/10 px-6 py-4 text-[clamp(1rem,1.3vw,1.35rem)] text-ivory">
                <p>{startError}</p>
                <button type="button" onClick={handleStart} className="tap min-h-[3.5rem] rounded-xl border-2 border-gold px-8 font-medium text-gold-light">
                  Yenidən cəhd et
                </button>
              </div>
            )}
          </div>
        </div>
      </section>

      <p className="absolute bottom-5 text-[clamp(0.8rem,1vw,1rem)] text-mist/60">Ekrana toxunaraq oynayın</p>
    </main>
  )
}
