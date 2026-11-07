import { reactive } from 'vue'

// ---------- پیام‌های کوتاه (toast) ----------
export const toasts = reactive([])
let seq = 0
export function toast(message, type = 'success') {
  const t = { id: ++seq, message, type }
  toasts.push(t)
  setTimeout(() => {
    const i = toasts.indexOf(t)
    if (i >= 0) toasts.splice(i, 1)
  }, type === 'error' ? 5000 : 3000)
}

// ---------- پنجرهٔ تأیید (جایگزین confirm مرورگر) ----------
export const confirmState = reactive({ open: false, title: '', message: '', confirmText: 'تأیید', danger: false, resolve: null })

export function confirmDialog(opts) {
  return new Promise((resolve) => {
    Object.assign(confirmState, { open: true, title: 'تأیید', message: '', confirmText: 'تأیید', danger: false, ...opts, resolve })
  })
}
export function answerConfirm(ok) {
  confirmState.open = false
  confirmState.resolve?.(ok)
}
