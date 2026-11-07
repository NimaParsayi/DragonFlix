// رنگ پوستر از روی عنوان ثابت می‌ماند، ولی همیشه در طیف آبی است.
export function hueOf(title = '') {
  let h = 0
  for (const c of title) h = (h * 31 + c.charCodeAt(0)) % 360
  return 205 + (h % 40) // ۲۰۵ تا ۲۴۵: فقط طیف آبی
}

// هر فیلم حداکثر ۳ ژانر دارد که با ویرگول ذخیره شده؛ اولی ژانر اصلی (آیکون پوستر) است.
export const genreList = (g) => (g || '').split(',').map((x) => x.trim()).filter(Boolean)

export const AGE_OPTIONS = [
  { v: '', label: 'نامشخص' },
  { v: 'all', label: 'همه سنین' },
  { v: '7', label: '۷ سال به بالا' },
  { v: '12', label: '۱۲ سال به بالا' },
  { v: '15', label: '۱۵ سال به بالا' },
  { v: '18', label: '۱۸ سال به بالا' },
]
export const ageLabel = (c) => ({ all: 'همه', 7: '+۷', 12: '+۱۲', 15: '+۱۵', 18: '+۱۸' })[c] ?? ''
