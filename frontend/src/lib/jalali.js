// تبدیل تقویم شمسی ↔ میلادی (الگوریتم jalaali-js)
const div = (a, b) => ~~(a / b)
const mod = (a, b) => a - ~~(a / b) * b
const BREAKS = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178]

function jalCal(jy) {
  const bl = BREAKS.length
  const gy = jy + 621
  let leapJ = -14, jp = BREAKS[0], jm, jump = 0, n, i
  for (i = 1; i < bl; i++) {
    jm = BREAKS[i]; jump = jm - jp
    if (jy < jm) break
    leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4)
    jp = jm
  }
  n = jy - jp
  leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4)
  if (mod(jump, 33) === 4 && jump - n === 4) leapJ += 1
  const leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150
  const march = 20 + leapJ - leapG
  if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33
  let leap = mod(mod(n + 1, 33) - 1, 4)
  if (leap === -1) leap = 4
  return { leap, gy, march }
}
const g2d = (gy, gm, gd) => {
  const d = div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408
  return d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752
}
const d2g = (jdn) => {
  let j = 4 * jdn + 139361631
  j += div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908
  const i = div(mod(j, 1461), 4) * 5 + 308
  const gd = div(mod(i, 153), 5) + 1
  const gm = mod(div(i, 153), 12) + 1
  return { gy: div(j, 1461) - 100100 + div(8 - gm, 6), gm, gd }
}
const j2d = (jy, jm, jd) => {
  const r = jalCal(jy)
  return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1
}
const d2j = (jdn) => {
  const gy = d2g(jdn).gy
  let jy = gy - 621
  const r = jalCal(jy)
  let k = jdn - g2d(gy, 3, r.march)
  if (k >= 0) {
    if (k <= 185) return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 }
    k -= 186
  } else {
    jy -= 1; k += 179
    if (r.leap === 1) k += 1
  }
  return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 }
}

export const toJalali = (gy, gm, gd) => d2j(g2d(gy, gm, gd))
export const toGregorian = (jy, jm, jd) => d2g(j2d(jy, jm, jd))
export const jalaliMonthLength = (jy, jm) => (jm === 12 ? j2d(jy + 1, 1, 1) : j2d(jy, jm + 1, 1)) - j2d(jy, jm, 1)
// روز هفته با شروع از شنبه = ۰
export const jalaliWeekday = (jy, jm, jd) => { const g = toGregorian(jy, jm, jd); return (new Date(g.gy, g.gm - 1, g.gd).getDay() + 1) % 7 }
export const MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند']
