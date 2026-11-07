<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { seatLines } from '../../lib/seat'
import { ref, onMounted, onBeforeUnmount, watch } from 'vue'
import Modal from '../../components/Modal.vue'
import Icon from '../../components/Icon.vue'
import { api, fetchObjectUrl } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { money, num, fmtDate, fmtTime, fmtFull } from '../../lib/format'
import * as tg from '../../lib/tg'
import { adminBadge } from '../../lib/adminBadge'

const TABS = [
  { key: 'awaiting', label: 'در انتظار تأیید' },
  { key: 'pending', label: 'منتظر پرداخت کاربر' },
  { key: 'recent', label: 'تصمیم‌گرفته‌شده' },
]
const STATUS = { AwaitingReview: ['در انتظار تأیید', 'info'], PendingPayment: ['منتظر پرداخت', 'warn'], Confirmed: ['تأییدشده', 'ok'], Rejected: ['ردشده', 'bad'], Expired: ['منقضی', 'mute'] }
const REASONS = ['رسید ناخوانا یا نامعتبر است', 'مبلغ رسید با مبلغ رزرو مطابقت ندارد', 'واریزی به حساب ما انجام نشده است']

const tab = ref('awaiting')
const data = ref(null)
const imgs = ref({}) // bookingId → object URL
const big = ref(null)
const rejecting = ref(null) // { id, reason }
const busy = ref(0)
let timer

async function load() {
  try {
    data.value = await api.get(`/api/admin/payments?status=${tab.value}`)
    adminBadge.pending = data.value.awaitingCount
    for (const p of data.value.items) {
      if (p.hasReceipt && !imgs.value[p.id]) fetchObjectUrl(`/api/bookings/${p.id}/receipt`).then((u) => (imgs.value[p.id] = u)).catch(() => {})
    }
  } catch (e) { toast(e.message, 'error') }
}
onMounted(() => { load(); timer = setInterval(() => !document.hidden && load(), 15000) })
onBeforeUnmount(() => { clearInterval(timer); Object.values(imgs.value).forEach(URL.revokeObjectURL) })
watch(tab, () => { data.value = null; load() })

async function approve(p) {
  const ok = await confirmDialog({ title: 'تأیید پرداخت', message: `پرداخت ${money(p.totalPrice)} از «${p.user.fullName}» تأیید و بلیت صادر شود؟`, confirmText: 'تأیید و صدور بلیت' })
  if (!ok) return
  busy.value = p.id
  try { const r = await api.post(`/api/admin/payments/${p.id}/approve`); toast(r.message); tg.haptic.success(); await load() }
  catch (e) { toast(e.message, 'error'); await load() } finally { busy.value = 0 }
}
async function reject() {
  const { id, reason } = rejecting.value
  busy.value = id
  try { const r = await api.post(`/api/admin/payments/${id}/reject`, { reason }); toast(r.message); rejecting.value = null; await load() }
  catch (e) { toast(e.message, 'error'); await load() } finally { busy.value = 0 }
}
</script>

<template>
  <div class="page-head">
    <div><h1>پرداخت‌ها</h1><p class="muted">رسیدهای کارت‌به‌کارت را بررسی کنید؛ با تأیید، بلیت برای کاربر صادر و در تلگرام ارسال می‌شود.</p></div>
  </div>

  <div class="tabs" role="tablist">
    <button v-for="t in TABS" :key="t.key" role="tab" :aria-selected="tab === t.key" :class="{ on: tab === t.key }" @click="tab = t.key">
      {{ t.label }}<b v-if="t.key === 'awaiting' && data?.awaitingCount" class="count">{{ num(data.awaitingCount) }}</b>
    </button>
  </div>

  <Skeleton v-if="!data" variant="panels" />
  <div v-else-if="!data.items.length" class="panel empty"><Icon name="check" :size="28" /><p>{{ tab === 'awaiting' ? 'رسید در انتظاری نیست.' : 'موردی برای نمایش نیست.' }}</p></div>

  <div v-else class="stack">
    <article v-for="p in data.items" :key="p.id" class="panel pay">
      <button v-if="p.hasReceipt" class="shot" :aria-label="`رسید ${p.id}`" @click="big = imgs[p.id]">
        <img v-if="imgs[p.id]" :src="imgs[p.id]" alt="رسید" />
        <span v-else class="sk-shot" />
      </button>
      <div v-else class="shot none"><Icon name="camera" :size="22" /><span class="muted small">بدون عکس</span></div>

      <div class="body">
        <div class="row between">
          <b class="who">{{ p.user.fullName }}
            <a v-if="p.user.username" class="muted small" :href="`https://t.me/${p.user.username}`" target="_blank" rel="noopener" dir="ltr">@{{ p.user.username }}</a>
          </b>
          <span class="st" :class="STATUS[p.status]?.[1]">{{ STATUS[p.status]?.[0] ?? p.status }}</span>
        </div>
        <div>{{ p.movie }} <span class="muted small">· {{ p.cinema }} / {{ p.hall }}</span></div>
        <div class="muted small">{{ fmtDate(p.startsAt) }} · {{ fmtTime(p.startsAt) }} · {{ seatLines(p.seatsText.split(',')).join('؛ ') }}</div>
        <div class="money">{{ money(p.totalPrice) }}</div>
        <div class="meta small">
          <span v-if="p.trackingCode">پیگیری: <b dir="ltr">{{ p.trackingCode }}</b></span>
          <span v-if="p.payerCard4">کارت: <b dir="ltr">···· {{ p.payerCard4 }}</b></span>
          <span class="muted">رزرو #{{ num(p.id) }}</span>
          <span v-if="p.submittedAt" class="muted">رسید: {{ fmtFull(p.submittedAt) }}</span>
        </div>
        <div v-if="p.rejectReason" class="reason small"><Icon name="alert" :size="13" /> {{ p.rejectReason }}</div>

        <div v-if="p.status === 'AwaitingReview' || p.status === 'PendingPayment'" class="acts">
          <button class="btn" :disabled="busy === p.id" @click="approve(p)"><Icon name="check" :size="16" /> تأیید و صدور بلیت</button>
          <button class="btn danger" :disabled="busy === p.id" @click="rejecting = { id: p.id, reason: '' }">رد کردن</button>
        </div>
      </div>
    </article>
  </div>

  <Modal v-if="big" title="عکس رسید" width="560px" @close="big = null"><img :src="big" alt="رسید" class="bigimg" /></Modal>

  <Modal v-if="rejecting" title="دلیل رد پرداخت" width="460px" @close="rejecting = null">
    <p class="muted small" style="margin: 0">دلیل برای کاربر ارسال می‌شود و صندلی‌ها آزاد می‌شوند.</p>
    <div class="reasons"><button v-for="r in REASONS" :key="r" type="button" :class="{ on: rejecting.reason === r }" @click="rejecting.reason = r">{{ r }}</button></div>
    <label class="field">یا دلیل دلخواه <textarea v-model="rejecting.reason" rows="2" maxlength="200" /></label>
    <template #footer>
      <button class="btn danger" :disabled="busy === rejecting.id" @click="reject">رد کردن پرداخت</button>
      <button class="btn secondary" @click="rejecting = null">انصراف</button>
    </template>
  </Modal>
</template>

<style scoped>
.tabs { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 16px; }
.tabs button { font: inherit; display: inline-flex; align-items: center; gap: 8px; padding: 6px 16px; border-radius: 999px; border: 1px solid var(--border); background: transparent; color: var(--muted); cursor: pointer; }
.tabs button.on { background: var(--primary-soft); border-color: #1f3a68; color: #9cc2ff; }
.count { min-width: 20px; height: 20px; border-radius: 999px; display: inline-grid; place-items: center; font-size: .72rem; background: var(--primary); color: #fff; padding: 0 6px; }
.empty { display: grid; justify-items: center; gap: 6px; padding: 40px; color: var(--muted); }
.empty p { margin: 0; }
.pay { display: grid; grid-template-columns: 120px minmax(0, 1fr); gap: 16px; padding: 16px; }
.shot { position: relative; width: 120px; height: 150px; border-radius: 10px; border: 1px solid var(--border); background: var(--surface-2); overflow: hidden; display: grid; place-items: center; cursor: zoom-in; padding: 0; }
.shot img { width: 100%; height: 100%; object-fit: cover; }
.shot.none { cursor: default; color: var(--muted); gap: 4px; align-content: center; }
.body { display: grid; gap: 4px; align-content: start; min-width: 0; }
.who { display: inline-flex; gap: 8px; align-items: baseline; flex-wrap: wrap; }
.money { font-size: 1.25rem; font-weight: 700; margin-top: 2px; }
.meta { display: flex; gap: 6px 16px; flex-wrap: wrap; }
.reason { display: flex; align-items: center; gap: 5px; color: #fca5a5; }
.acts { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 8px; }
.st { padding: 0 11px; border-radius: 999px; font-size: .75rem; border: 1px solid; white-space: nowrap; }
.st.ok { background: #0f2619; color: #86efac; border-color: #1d4a2e; }
.st.warn { background: #33270e; color: #f0b952; border-color: #5a4514; }
.st.info { background: var(--primary-soft); color: #9cc2ff; border-color: #1f3a68; }
.st.bad { background: #2a1417; color: #fca5a5; border-color: #4a2428; }
.st.mute { background: var(--surface-2); color: var(--muted); border-color: var(--border); }
.bigimg { width: 100%; border-radius: 10px; display: block; }
.reasons { display: grid; gap: 6px; }
.reasons button { font: inherit; text-align: start; padding: 8px 12px; border-radius: 10px; border: 1px solid var(--border); background: transparent; color: var(--text); cursor: pointer; }
.reasons button.on { border-color: var(--primary); background: var(--primary-soft); }
@media (max-width: 640px) { .pay { grid-template-columns: 90px minmax(0, 1fr); gap: 12px; } .shot { width: 90px; height: 120px; } }
.sk-shot { position: absolute; inset: 0; background: linear-gradient(90deg, var(--surface-2) 30%, #1d2535 50%, var(--surface-2) 70%); background-size: 200% 100%; animation: shot 1.4s linear infinite; }
@keyframes shot { to { background-position: -200% 0; } }
</style>
