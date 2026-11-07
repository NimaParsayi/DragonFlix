import { reactive } from 'vue'
import { initData, inTelegram } from './tg'

// ورود فقط با نشست تلگرام است: هر بار که مینی‌اپ باز می‌شود initData امضاشده به سرور می‌رود
// و توکن فقط در حافظهٔ همین صفحه می‌ماند (ذخیره‌ای در مرورگر نیست).
let readyResolve
const ready = new Promise((r) => (readyResolve = r))

export const auth = reactive({
  token: null,
  user: null,
  ready: false,
  error: '',
  get isAdmin() { return this.user?.role === 'Admin' },
  get inTelegram() { return inTelegram() },
  whenReady: () => ready,

  async bootstrap() {
    this.error = ''
    const data = initData()
    if (!data) { this.token = null; this.user = null; this.finish(); return false }
    try {
      const res = await fetch('/api/auth/telegram', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ initData: data }) })
      const body = await res.json().catch(() => null)
      if (!res.ok) throw new Error(body?.message || 'ورود با تلگرام ناموفق بود.')
      this.token = body.token
      this.user = body.user
      this.finish()
      return true
    } catch (e) {
      this.token = null; this.user = null
      this.error = e.message === 'Failed to fetch' ? 'ارتباط با سرور برقرار نشد.' : e.message
      this.finish()
      return false
    }
  },
  async finish() {
    // تا فونت اصلی نیامده اسکلت می‌ماند تا متن‌ها جابه‌جا نشوند (حداکثر ۲ ثانیه)
    try { await Promise.race([document.fonts?.ready, new Promise((r) => setTimeout(r, 2000))]) } catch { /* ادامه */ }
    this.ready = true
    readyResolve()
  },
})
