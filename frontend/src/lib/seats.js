export const SEAT_STATUS = {
  free: 'آزاد',
  selected: 'انتخاب تو',
  held: 'در حال رزرو',
  taken: 'رزرو شده',
  disabled: 'غیرفعال',
}

export const SEAT_HINT = {
  free: 'قابل انتخاب است',
  selected: 'برای لغو، دوباره بزن',
  held: 'یه نفر دیگه داره رزروش می‌کنه',
  taken: 'قبلاً رزرو شده',
  disabled: 'قابل فروش نیست',
}

export function seatStatus(row, n, selectedSet) {
  if (row.taken?.includes(n)) return 'taken'
  if (row.disabled?.includes(n)) return 'disabled'
  if (row.held?.includes(n)) return 'held'
  if (selectedSet?.has(n)) return 'selected'
  return 'free'
}
