import { reactive } from 'vue'
import { api } from './api'

// تعداد رسیدهای منتظر تأیید؛ منوی کناری و صفحهٔ پرداخت‌ها هر دو آن را به‌روز می‌کنند
export const adminBadge = reactive({
  pending: 0,
  async refresh() { try { this.pending = (await api.get('/api/admin/stats')).pendingReview } catch { /* بی‌صدا */ } },
})
