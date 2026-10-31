import type { ReactNode } from 'react'
import ArenaBackdrop from '../arena/ArenaBackdrop'
import { FlagStripe } from '../arena/NationalMotifs'
import HomeAtmosphere from './HomeAtmosphere'
import HomeHeader from './HomeHeader'

/**
 * Page frame for every information screen: the arena background, the brand bar and a content column.
 * `atmosphere` adds the home page's layered background (HomeAtmosphere); every other screen keeps the
 * plain arena, which is also what the game itself stands on.
 */
export default function GameShowShell({ children, atmosphere = false }: { children: ReactNode; atmosphere?: boolean }) {
  return (
    <main className={`kiosk kiosk-scroll arena flex flex-col${atmosphere ? ' arena-atmosphere' : ''}`}>
      {atmosphere && <HomeAtmosphere />}
      <FlagStripe className="relative z-20 shrink-0" />
      <ArenaBackdrop />
      <HomeHeader />
      {/* safe center: content taller than the space grows downwards instead of sliding under the header */}
      <div className="relative z-0 mx-auto flex min-h-0 w-full max-w-[112rem] flex-1 flex-col gap-[clamp(1rem,2.2vh,1.6rem)] px-[clamp(1.2rem,3vw,3rem)] pb-[clamp(1.2rem,3vh,2.4rem)] pt-[clamp(1rem,2.4vh,1.8rem)] [justify-content:safe_center] max-sm:justify-start max-sm:gap-4 max-sm:px-4 max-sm:pb-[calc(1.2rem_+_var(--safe-bottom))] max-sm:pt-4">
        {children}
      </div>
    </main>
  )
}
