// بوق اسکن: صدای کوتاه و بالا برای موفقیت، صدای دوتایی و بم برای خطا. بدون فایل صوتی (Web Audio).
const KEY = 'dragonflix.sound'
let ctx = null

export const soundEnabled = () => {
  try { return localStorage.getItem(KEY) !== 'off' } catch { return true }
}
export const setSoundEnabled = (on) => {
  try { localStorage.setItem(KEY, on ? 'on' : 'off') } catch { /* ignore */ }
}

// مرورگر فقط بعد از یک کنش کاربر اجازهٔ پخش صدا می‌دهد؛ با کلیک روی «اسکن» صدا آماده می‌شود.
export function unlockAudio() {
  try {
    ctx ??= new (window.AudioContext || window.webkitAudioContext)()
    if (ctx.state === 'suspended') ctx.resume()
  } catch { /* ignore */ }
}

function tone(freq, start, dur, type = 'sine', vol = 0.28) {
  const o = ctx.createOscillator()
  const g = ctx.createGain()
  o.type = type
  o.frequency.value = freq
  o.connect(g)
  g.connect(ctx.destination)
  const t = ctx.currentTime + start
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(vol, t + 0.015)
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  o.start(t)
  o.stop(t + dur + 0.03)
}

export function beepOk() {
  navigator.vibrate?.(60)
  if (!soundEnabled()) return
  try { unlockAudio(); tone(1175, 0, 0.12); tone(1568, 0.11, 0.18) } catch { /* ignore */ }
}

export function beepFail() {
  navigator.vibrate?.([140, 70, 140])
  if (!soundEnabled()) return
  try { unlockAudio(); tone(220, 0, 0.2, 'square', 0.2); tone(165, 0.24, 0.32, 'square', 0.2) } catch { /* ignore */ }
}
