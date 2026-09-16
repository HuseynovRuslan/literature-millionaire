import AnswerShape from './AnswerShape'
import { Buta, Octagram } from './NationalMotifs'

/** Four big answer shapes drifting behind the content: the quiz identity, kept very faint. */
export default function ArenaBackdrop() {
  return (
    <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden" aria-hidden="true">
      <AnswerShape option="A" className="arena-shape left-[4%] top-[18%] size-[clamp(5rem,10vw,9rem)] text-opt-a" />
      <AnswerShape option="B" className="arena-shape right-[6%] top-[8%] size-[clamp(4rem,8vw,7rem)] text-opt-b [animation-delay:-4s]" />
      <AnswerShape option="C" className="arena-shape bottom-[10%] left-[10%] size-[clamp(3.5rem,7vw,6rem)] text-sun [animation-delay:-8s]" />
      <AnswerShape option="D" className="arena-shape bottom-[16%] right-[8%] size-[clamp(4.5rem,9vw,8rem)] text-opt-d [animation-delay:-12s]" />
      <Octagram className="arena-shape left-[46%] top-[4%] size-[clamp(3rem,5vw,4.5rem)] text-sun [animation-delay:-6s]" />
      <Buta className="arena-shape bottom-[4%] left-[44%] h-[clamp(4.5rem,8vw,7rem)] w-auto text-opt-a [animation-delay:-10s]" />
    </div>
  )
}
