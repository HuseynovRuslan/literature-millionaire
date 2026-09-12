// Səs faylı olmadan, WebAudio ilə sadə oyun səsləri
let ctx = null
let muted = false

function ac() {
  if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)()
  if (ctx.state === 'suspended') ctx.resume()
  return ctx
}

function tone(freq, start, dur, { type = 'sine', vol = 0.18 } = {}) {
  const c = ac()
  const t = c.currentTime + start
  const osc = c.createOscillator()
  const gain = c.createGain()
  osc.type = type
  osc.frequency.setValueAtTime(freq, t)
  gain.gain.setValueAtTime(0.0001, t)
  gain.gain.exponentialRampToValueAtTime(vol, t + 0.02)
  gain.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  osc.connect(gain).connect(c.destination)
  osc.start(t)
  osc.stop(t + dur + 0.05)
}

function play(fn) {
  if (muted) return
  try {
    fn()
  } catch {
    /* səs dəstəklənmirsə, səssiz davam et */
  }
}

export const sound = {
  setMuted(v) {
    muted = v
  },
  isMuted: () => muted,
  tap: () => play(() => tone(660, 0, 0.08, { type: 'triangle', vol: 0.12 })),
  tick: () => play(() => tone(1200, 0, 0.05, { type: 'square', vol: 0.05 })),
  correct: () =>
    play(() => {
      tone(523, 0, 0.14, { type: 'triangle' })
      tone(659, 0.1, 0.14, { type: 'triangle' })
      tone(784, 0.2, 0.3, { type: 'triangle' })
    }),
  wrong: () =>
    play(() => {
      tone(220, 0, 0.25, { type: 'sawtooth', vol: 0.1 })
      tone(165, 0.18, 0.35, { type: 'sawtooth', vol: 0.1 })
    }),
  finish: () =>
    play(() => {
      ;[523, 659, 784, 1047].forEach((f, i) => tone(f, i * 0.13, 0.35, { type: 'triangle', vol: 0.15 }))
    }),
}
