const nf = new Intl.NumberFormat('fa-IR')

export const money = (v) => `${nf.format(Math.round(v ?? 0))} تومان`
export const num = (v) => nf.format(v)

const dateFmt = new Intl.DateTimeFormat('fa-IR', { weekday: 'long', day: 'numeric', month: 'long' })
const timeFmt = new Intl.DateTimeFormat('fa-IR', { hour: '2-digit', minute: '2-digit', hour12: false })
const fullFmt = new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short', hour12: false })

// سرور زمان را «به وقت محلی سینما» و بدون منطقهٔ زمانی برمی‌گرداند؛ مرورگر آن را محلی می‌خواند.
export const fmtDate = (iso) => dateFmt.format(new Date(iso))
export const fmtTime = (iso) => timeFmt.format(new Date(iso))
export const fmtFull = (iso) => fullFmt.format(new Date(iso))
export const dayKey = (iso) => iso.slice(0, 10)

export function toInputValue(iso) {
  return iso ? iso.slice(0, 16) : ''
}

// کد ۸ کاراکتری بلیت را به شکل XXXX-XXXX نشان می‌دهد
export const fmtCode = (c = '') => (c.length === 8 ? `${c.slice(0, 4)}-${c.slice(4)}` : c)
