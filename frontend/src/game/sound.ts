/**
 * Atmosphere for the quiz screen: a heartbeat that quickens as the round climbs and the clock runs
 * down, a low tension bed under it, and short cues when an answer is locked in or the quiz ends.
 *
 * Everything is synthesised with the Web Audio API, so the kiosk ships no audio files and there is no
 * third-party music to licence. Nothing here reveals anything about the quiz: the cues depend only on
 * the question number and the seconds left, never on whether an answer was right.
 *
 * Browsers refuse to start audio before a user gesture; the engine therefore stays silent until
 * `resume()` is called from a tap (registration, or the answer buttons), and every call is a no-op when
 * the device has no Web Audio support.
 */

const STORAGE_KEY = 'lm.sound.enabled'

/** Heartbeat period at the calmest and the tensest moment of a round. */
const BEAT_SLOWEST_MS = 1250
const BEAT_FASTEST_MS = 430

let context: AudioContext | null = null
let master: GainNode | null = null
let beatTimer: number | null = null
let beatIntensity = 0
let dronePlaying = false
let droneNodes: { osc: OscillatorNode[]; gain: GainNode } | null = null
let enabled = readPreference()

function readPreference(): boolean {
  try {
    // Per-device convenience only: a venue can mute the kiosk and have it stay muted.
    return localStorage.getItem(STORAGE_KEY) !== 'off'
  } catch {
    return true
  }
}

function ensureContext(): AudioContext | null {
  if (!enabled) return null
  if (context) return context
  const Ctor = window.AudioContext ?? (window as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!Ctor) return null
  try {
    context = new Ctor()
    master = context.createGain()
    master.gain.value = 0.55
    master.connect(context.destination)
    return context
  } catch {
    return null
  }
}

/** True while the kiosk is allowed to make sound. */
export function isEnabled(): boolean {
  return enabled
}

/** Turns the atmosphere on or off and remembers the choice on this device. */
export function setEnabled(next: boolean): void {
  enabled = next
  try {
    localStorage.setItem(STORAGE_KEY, next ? 'on' : 'off')
  } catch {
    /* storage unavailable: the choice simply lasts for this session */
  }
  if (!next) stopAll()
}

/** Call from a user gesture; browsers keep audio suspended until one happens. */
export function resume(): void {
  const ctx = ensureContext()
  if (ctx && ctx.state === 'suspended') void ctx.resume()
}

/** Silences everything and releases the timers. Safe to call at any time. */
export function stopAll(): void {
  if (beatTimer !== null) {
    window.clearTimeout(beatTimer)
    beatTimer = null
  }
  stopDrone()
}

// --- building blocks ---------------------------------------------------------

/** One heart thump: a short sine that drops in pitch, like a drum. */
function thump(at: number, gainPeak: number): void {
  const ctx = context
  if (!ctx || !master) return
  const osc = ctx.createOscillator()
  const gain = ctx.createGain()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(74, at)
  osc.frequency.exponentialRampToValueAtTime(38, at + 0.11)
  gain.gain.setValueAtTime(0.0001, at)
  gain.gain.exponentialRampToValueAtTime(gainPeak, at + 0.012)
  gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.17)
  osc.connect(gain).connect(master)
  osc.start(at)
  osc.stop(at + 0.2)
}

/** The low bed that sits under the heartbeat; louder as the round climbs. */
function startDrone(level: number): void {
  const ctx = ensureContext()
  if (!ctx || !master) return
  if (dronePlaying && droneNodes) {
    droneNodes.gain.gain.setTargetAtTime(0.012 + level * 0.03, ctx.currentTime, 0.6)
    return
  }
  const gain = ctx.createGain()
  gain.gain.value = 0.0001
  gain.gain.setTargetAtTime(0.012 + level * 0.03, ctx.currentTime, 0.8)
  const filter = ctx.createBiquadFilter()
  filter.type = 'lowpass'
  filter.frequency.value = 320
  const osc = [55, 55.4, 110].map((frequency, index) => {
    const o = ctx.createOscillator()
    o.type = index === 2 ? 'triangle' : 'sine'
    o.frequency.value = frequency
    o.connect(filter)
    o.start()
    return o
  })
  filter.connect(gain).connect(master)
  droneNodes = { osc, gain }
  dronePlaying = true
}

function stopDrone(): void {
  if (!droneNodes || !context) {
    dronePlaying = false
    return
  }
  const { osc, gain } = droneNodes
  const now = context.currentTime
  gain.gain.setTargetAtTime(0.0001, now, 0.2)
  osc.forEach((o) => o.stop(now + 0.9))
  droneNodes = null
  dronePlaying = false
}

// --- the round ---------------------------------------------------------------

function scheduleBeat(): void {
  const ctx = context
  if (!ctx || !enabled) return
  const now = ctx.currentTime
  const peak = 0.10 + beatIntensity * 0.22
  thump(now, peak)
  thump(now + 0.21, peak * 0.62)

  const period = BEAT_SLOWEST_MS + (BEAT_FASTEST_MS - BEAT_SLOWEST_MS) * beatIntensity
  beatTimer = window.setTimeout(scheduleBeat, period)
}

/**
 * Keeps the atmosphere in step with the game.
 *
 * @param questionNumber 1..totalQuestions - the step of the climb.
 * @param totalQuestions how many questions the round has.
 * @param secondsLeft seconds still on the clock for the visible question.
 * @param secondsPerQuestion the question's full time budget.
 */
export function updateRound(questionNumber: number, totalQuestions: number, secondsLeft: number, secondsPerQuestion: number): void {
  const ctx = ensureContext()
  if (!ctx) return

  // Two thirds of the tension come from the clock, one third from how high the round has climbed.
  const climb = totalQuestions > 1 ? (questionNumber - 1) / (totalQuestions - 1) : 0
  const urgency = secondsPerQuestion > 0 ? 1 - Math.max(0, Math.min(1, secondsLeft / secondsPerQuestion)) : 0
  beatIntensity = Math.max(0, Math.min(1, climb * 0.35 + urgency * 0.65))

  startDrone(climb)
  if (beatTimer === null) scheduleBeat()
}

/** The answer is in and the question is closed: a short lock-in, then the beat holds under the pause. */
export function lockIn(): void {
  const ctx = ensureContext()
  if (!ctx || !master) return
  const now = ctx.currentTime
  const osc = ctx.createOscillator()
  const gain = ctx.createGain()
  osc.type = 'triangle'
  osc.frequency.setValueAtTime(620, now)
  osc.frequency.exponentialRampToValueAtTime(190, now + 0.22)
  gain.gain.setValueAtTime(0.0001, now)
  gain.gain.exponentialRampToValueAtTime(0.16, now + 0.02)
  gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.3)
  osc.connect(gain).connect(master)
  osc.start(now)
  osc.stop(now + 0.32)
  // The pause that follows is the tense part, so the heartbeat stays near its peak while it lasts.
  beatIntensity = Math.max(beatIntensity, 0.8)
}

/** The quiz is over. Plays once, then everything goes quiet. */
export function finish(passed: boolean): void {
  const ctx = ensureContext()
  stopAll()
  if (!ctx || !master) return
  const out = master
  const now = ctx.currentTime
  const notes = passed ? [392, 523.25, 659.25, 783.99] : [392, 311.13, 233.08]
  notes.forEach((frequency, index) => {
    const at = now + index * (passed ? 0.16 : 0.26)
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = passed ? 'triangle' : 'sine'
    osc.frequency.setValueAtTime(frequency, at)
    gain.gain.setValueAtTime(0.0001, at)
    gain.gain.exponentialRampToValueAtTime(0.14, at + 0.03)
    gain.gain.exponentialRampToValueAtTime(0.0001, at + (passed ? 0.5 : 0.7))
    osc.connect(gain).connect(out)
    osc.start(at)
    osc.stop(at + (passed ? 0.55 : 0.75))
  })
}
