// پوشش‌دهندهٔ SDK مینی‌اپ تلگرام (window.Telegram.WebApp).
// اگر SDK لود نشده باشد (فیلتر بودن telegram.org، یا تست در مرورگر) فقط initData را از hash آدرس می‌خوانیم
// و بقیهٔ قابلیت‌ها بی‌صدا نادیده گرفته می‌شوند؛ برنامه هیچ‌وقت به خاطر نبودن آن‌ها خراب نمی‌شود.

const w = () => window.Telegram?.WebApp
const KEY = 'dragonflix.tgInit'

/** واقعاً داخل برنامهٔ تلگرام اجرا می‌شویم؟ (وب‌ویوی بومی یا iframe تلگرام وب) — نه فقط SDK لودشده در یک مرورگر معمولی */
const hosted = () => !!(window.TelegramWebviewProxy || window.parent !== window)

/** رشتهٔ امضاشدهٔ initData که سرور اعتبارش را با توکن ربات بررسی می‌کند. */
export function initData() {
  if (w()?.initData) return w().initData
  try {
    const fromHash = new URLSearchParams(location.hash.replace(/^#/, '')).get('tgWebAppData')
    if (fromHash) sessionStorage.setItem(KEY, fromHash)
    return fromHash || sessionStorage.getItem(KEY) || ''
  } catch { return '' }
}

export const inTelegram = () => !!initData()
export const botUsername = import.meta.env.VITE_BOT_USERNAME || 'DragonFlixApp'
export const botLink = `https://t.me/${botUsername}`

const BG = '#0b0d12'

export function setup() {
  const app = w()
  if (!app) return
  try {
    app.ready()
    app.expand()                      // تمام‌قد
    // تمام‌صفحه (Bot API 8.0+) فقط روی موبایل؛ روی دسکتاپ پنجرهٔ معمولی بهتر است
    if (['android', 'ios'].includes(app.platform) && app.isVersionAtLeast?.('8.0') && !app.isFullscreen) app.requestFullscreen?.()
    app.setHeaderColor?.(BG)
    app.setBackgroundColor?.(BG)
    app.setBottomBarColor?.(BG)
    app.disableVerticalSwipes?.()     // کشیدن به پایین ناخواسته مینی‌اپ را نبندد (مثل کشیدن اسلایدر)
  } catch { /* نسخهٔ قدیمی تلگرام */ }
}

// ---------- لرزش لمسی ----------
const hf = () => w()?.HapticFeedback
export const haptic = {
  tap: () => hf()?.impactOccurred?.('light'),
  select: () => hf()?.selectionChanged?.(),
  success: () => hf()?.notificationOccurred?.('success'),
  warn: () => hf()?.notificationOccurred?.('warning'),
  error: () => hf()?.notificationOccurred?.('error'),
}

// ---------- دکمهٔ بازگشت بومی تلگرام ----------
let backHandler = null
export const backButton = {
  show(fn) {
    const b = w()?.BackButton
    if (!b) return
    if (backHandler) b.offClick(backHandler)
    backHandler = fn
    b.onClick(fn)
    b.show()
  },
  hide() {
    const b = w()?.BackButton
    if (!b) return
    if (backHandler) { b.offClick(backHandler); backHandler = null }
    b.hide()
  },
}

// ---------- دکمهٔ اصلی پایین (MainButton) ----------
let mainHandler = null
export const mainButton = {
  /** فقط وقتی داخل تلگرام واقعی هستیم و SDK دکمه را دارد */
  get supported() { return !!(w()?.MainButton && w()?.initData && hosted()) },
  show({ text, onClick, loading = false, disabled = false }) {
    const b = w()?.MainButton
    if (!b) return
    if (mainHandler) b.offClick(mainHandler)
    mainHandler = onClick
    b.onClick(onClick)
    b.setParams({ text, color: '#3b82f6', text_color: '#ffffff', is_active: !disabled && !loading, is_visible: true })
    loading ? b.showProgress(false) : b.hideProgress()
  },
  hide() {
    const b = w()?.MainButton
    if (!b) return
    if (mainHandler) { b.offClick(mainHandler); mainHandler = null }
    b.hideProgress()
    b.hide()
  },
}

/** پنجرهٔ «ارسال به…» تلگرام؛ بیرون از تلگرام Web Share یا کپی لینک. */
export async function shareLink(url, text = '') {
  const share = `https://t.me/share/url?url=${encodeURIComponent(url)}&text=${encodeURIComponent(text)}`
  if (hosted() && w()?.openTelegramLink) { w().openTelegramLink(share); return 'telegram' }
  if (navigator.share) { try { await navigator.share({ url, text }); return 'native' } catch { return 'cancel' } }
  return (await copyText(url)) ? 'copied' : 'failed'
}

export function openTelegramLink(url) {
  if (w()?.openTelegramLink) w().openTelegramLink(url)
  else window.open(url, '_blank', 'noopener')
}

/** کپی در کلیپ‌بورد؛ true اگر موفق بود. */
export async function copyText(text) {
  try { await navigator.clipboard.writeText(text); return true } catch { /* ادامه: روش قدیمی */ }
  try {
    const ta = Object.assign(document.createElement('textarea'), { value: text })
    ta.style.cssText = 'position:fixed;opacity:0'
    document.body.appendChild(ta); ta.select()
    const ok = document.execCommand('copy'); ta.remove(); return ok
  } catch { return false }
}
