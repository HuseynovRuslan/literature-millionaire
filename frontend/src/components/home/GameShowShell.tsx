import type { ReactNode } from 'react'
import CarpetFrame from '../national/CarpetFrame'
import HomeHeader from './HomeHeader'

/**
 * Page frame shared by the game-show screens (home, registration): paper page with the carpet border,
 * the KİTABXANA 2.0 brand bar and a vertically centred content column.
 */
export default function GameShowShell({ children }: { children: ReactNode }) {
  return (
    <main className="kiosk kiosk-scroll paper flex flex-col">
      <CarpetFrame />
      <HomeHeader />
      {/* safe center: content taller than the space grows downwards instead of sliding under the header */}
      <div className="relative z-0 mx-auto flex min-h-0 w-full max-w-[118rem] flex-1 flex-col [justify-content:safe_center] gap-[1.3813rem] px-[calc(var(--frame)_+_2rem)] pb-[calc(var(--frame)_+_1rem)] pt-[0.85rem] max-sm:justify-start max-sm:gap-4 max-sm:px-[calc(var(--frame)_+_0.75rem)] max-sm:pb-[calc(var(--frame)_+_1rem_+_var(--safe-bottom))] max-sm:pt-3">
        {children}
      </div>
    </main>
  )
}
