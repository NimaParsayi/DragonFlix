import { reactive } from 'vue'

// پوشش تمام‌صفحهٔ «در حال انجام…» برای کارهای مهم (ثبت رزرو، ارسال رسید، لغو).
// تا پایان کار هر کلیک دیگری نادیده گرفته می‌شود و کاربر همیشه می‌بیند در چه مرحله‌ای است.
export const busy = reactive({ open: false, text: '', sub: '' })
let running = false

export const isBusy = () => running

/** fn(step) را اجرا می‌کند؛ اگر کار دیگری در جریان باشد اجرا نمی‌شود (undefined برمی‌گرداند). */
export async function runBusy(text, fn, sub = '') {
  if (running) return undefined
  running = true
  Object.assign(busy, { open: true, text, sub })
  try {
    return await fn((t, s = '') => Object.assign(busy, { text: t, sub: s }))
  } finally {
    running = false
    busy.open = false
  }
}
