import { reactive } from 'vue'

// وضعیت ناوبری: از لحظهٔ کلیک تا لود کامل صفحهٔ مقصد (برای نوار پیشرفت و فعال‌شدن فوری تب‌ها)
export const nav = reactive({ loading: false, to: null })
