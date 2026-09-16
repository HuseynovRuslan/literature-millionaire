import { startTransition, useEffect, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { getPlantCredits } from '../api/plants'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import type { PlantCredits } from '../types/plant'

type CreditsLoad =
  | { kind: 'loading' }
  | { kind: 'ready'; data: PlantCredits }
  | { kind: 'empty' }
  | { kind: 'error' }

/** Centred message block inside the stage (loading, empty, error). */
function StageMessage({ children, role }: { children: ReactNode; role?: 'status' | 'alert' }) {
  return (
    <div
      role={role}
      aria-live={role === 'status' ? 'polite' : undefined}
      className="flex min-h-[clamp(14rem,36vh,24rem)] flex-col items-center justify-center px-6 text-center max-sm:min-h-[14rem] max-sm:px-2"
    >
      {children}
    </div>
  )
}

const MESSAGE_TITLE = 'font-display text-[clamp(1.6rem,2.6vw,2.8rem)] font-bold text-[#fbf6ec] max-sm:text-[1.5rem]'
const MESSAGE_TEXT = 'mt-2 max-w-[40rem] text-[clamp(1rem,1.3vw,1.4rem)] text-[#d6deec] max-sm:text-[0.95rem]'

/**
 * Public credits for the plant photographs: author, source and licence of every picture the plant quiz
 * can show. The Creative Commons licences these pictures carry require the attribution to stay visible,
 * so this page is reachable without playing and is listed from the category screen.
 */
export default function PlantCreditsPage() {
  const navigate = useNavigate()
  const [load, setLoad] = useState<CreditsLoad>({ kind: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const navigationLocked = useRef(false)

  // The state starts as "loading" and the retry button puts it back there, so nothing has to be set
  // synchronously from inside the effect.
  useEffect(() => {
    const controller = new AbortController()
    getPlantCredits(controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return
        setLoad(data.images.length === 0 ? { kind: 'empty' } : { kind: 'ready', data })
      })
      .catch(() => { if (!controller.signal.aborted) setLoad({ kind: 'error' }) })
    return () => controller.abort()
  }, [attempt])

  function goHome() {
    if (navigationLocked.current) return
    navigationLocked.current = true
    startTransition(() => navigate('/'))
  }

  return (
    <GameShowShell>
      <section
        data-testid="credits-stage"
        aria-labelledby="credits-title"
        className="home-stage rise flex min-h-0 flex-col rounded-[clamp(1.2rem,1.6vw,2rem)] px-[clamp(1.2rem,2.8vw,3.6rem)] py-[clamp(0.9rem,2.2vh,2rem)] max-sm:rounded-2xl max-sm:px-3 max-sm:py-4"
      >
        <header className="shrink-0 px-[clamp(0rem,0.6vw,0.8rem)]">
          <p className="text-[clamp(0.85rem,1vw,1.15rem)] font-semibold uppercase tracking-[0.16em] text-[var(--p-gold-light)] max-sm:text-[0.75rem]">
            Yaşıl Bakı
          </p>
          <h1 id="credits-title" lang="az" className="font-display text-[clamp(2rem,min(3.4vw,5.6vh),4rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[1.8rem]">
            Şəkil mənbələri
          </h1>
          <p lang="az" className="mt-1 max-w-[70ch] text-[clamp(0.9rem,1.1vw,1.15rem)] leading-snug text-[#d6deec] max-sm:text-[0.85rem]">
            Bitki tanıma yarışmasında istifadə olunan fotoların müəllif, mənbə və lisenziya məlumatları.
            {load.kind === 'ready' && ` ${load.data.plantCount} bitki, ${load.data.imageCount} foto.`}
          </p>
        </header>

        <div className="mt-[clamp(0.5rem,1.4vh,1.1rem)] min-h-0 flex-1 overflow-y-auto">
          {load.kind === 'loading' && (
            <StageMessage role="status">
              <span className="spin inline-block h-10 w-10 rounded-full border-4 border-white/20 border-t-[var(--p-gold-light)]" aria-hidden />
              <p className={`${MESSAGE_TITLE} mt-4`}>Mənbələr yüklənir…</p>
            </StageMessage>
          )}

          {load.kind === 'error' && (
            <StageMessage role="alert">
              <p className={MESSAGE_TITLE}>Mənbələri yükləmək mümkün olmadı</p>
              <p className={MESSAGE_TEXT}>Şəbəkə bağlantısını yoxlayın və yenidən cəhd edin.</p>
              <button
                type="button"
                onClick={() => { setLoad({ kind: 'loading' }); setAttempt((a) => a + 1) }}
                className={`${SECONDARY_CTA} mt-5 min-h-[clamp(4rem,7vh,5rem)]! px-10`}
              >
                Yenidən yoxla
              </button>
            </StageMessage>
          )}

          {load.kind === 'empty' && (
            <StageMessage>
              <p className={MESSAGE_TITLE}>Hazırda bitki fotosu yoxdur</p>
              <p className={MESSAGE_TEXT}>Kataloq hazırlananda mənbələr burada görünəcək.</p>
            </StageMessage>
          )}

          {load.kind === 'ready' && (
            <ul data-testid="credits-list" className="flex flex-col gap-[clamp(0.3rem,0.7vh,0.5rem)]">
              {load.data.images.map((image) => (
                <li
                  key={image.fileName}
                  className="grid grid-cols-[auto_minmax(0,1fr)] items-center gap-[clamp(0.7rem,1.1vw,1.1rem)] rounded-xl bg-white/[0.06] px-[clamp(0.6rem,1vw,1rem)] py-[clamp(0.4rem,0.9vh,0.7rem)] ring-1 ring-white/10 max-sm:gap-2.5 max-sm:px-2.5"
                >
                  <img
                    src={image.imageUrl}
                    alt=""
                    loading="lazy"
                    className="size-[clamp(2.6rem,4vw,3.6rem)] shrink-0 rounded-lg border border-[rgba(233,192,105,0.5)] object-cover max-sm:size-11"
                  />
                  <div className="min-w-0">
                    <p lang="az" className="truncate font-semibold text-[clamp(0.95rem,1.15vw,1.2rem)] text-[#fbf6ec] max-sm:text-[0.9rem]">
                      {image.plantName}
                      <span className="ml-2 font-normal italic text-[#c9d3e6]">{image.specimenSpecies ?? image.scientificName}</span>
                    </p>
                    <p className="truncate text-[clamp(0.78rem,0.92vw,0.95rem)] text-[#c9d3e6] max-sm:text-[0.75rem]">
                      <span className="text-[var(--p-gold-light)]">{image.fileName}</span>
                      {' · '}
                      {image.author ?? 'müəllif göstərilməyib'}
                      {' · '}
                      {image.sourceUrl ? (
                        <a href={image.sourceUrl} target="_blank" rel="noreferrer noopener" className="underline decoration-[var(--p-gold)] underline-offset-2">
                          {image.source}
                        </a>
                      ) : (
                        image.source
                      )}
                      {' · '}
                      {image.licenseUrl ? (
                        <a href={image.licenseUrl} target="_blank" rel="noreferrer noopener" className="underline decoration-[var(--p-gold)] underline-offset-2">
                          {image.license}
                        </a>
                      ) : (
                        image.license
                      )}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      <nav aria-label="Mənbələr seçimləri" className="rise flex w-full max-w-[78rem] items-stretch justify-center self-center [animation-delay:90ms]">
        <button type="button" onClick={goHome} className={`${PRIMARY_CTA} w-full max-w-[34rem]`} data-testid="credits-home">
          ANA SƏHİFƏ
        </button>
      </nav>
    </GameShowShell>
  )
}
