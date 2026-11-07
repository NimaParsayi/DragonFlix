import { num } from './format'

// «ردیف G - صندلی ۶ و ۷» به‌جای «G6, G7»
export function parseSeat(label) {
  const m = /^(.*?)(\d+)$/.exec(String(label).trim())
  return m ? { row: m[1], number: Number(m[2]) } : { row: String(label).trim(), number: 0 }
}

// seats: [{row, number}] یا ["A1", ...] ؛ خروجی: یک رشته برای هر ردیف
export function seatLines(seats) {
  const groups = new Map()
  for (const s of seats || []) {
    const { row, number } = typeof s === 'string' ? parseSeat(s) : s
    if (!groups.has(row)) groups.set(row, [])
    groups.get(row).push(number)
  }
  return [...groups].map(([row, ns]) => {
    ns.sort((a, b) => a - b)
    const f = ns.map((n) => num(n))
    const list = f.length === 1 ? f[0] : f.slice(0, -1).join('، ') + ' و ' + f[f.length - 1]
    return `ردیف ${row} - ${f.length === 1 ? 'صندلی' : 'صندلی‌های'} ${list}`
  })
}

export const seatsFromText = (t) => (t || '').split(',').filter(Boolean)

export function seatOne(s) {
  const { row, number } = typeof s === 'string' ? parseSeat(s) : s
  return `ردیف ${row} - صندلی ${num(number)}`
}
