<script setup>
import Skeleton from '../components/Skeleton.vue'
import { seatLines } from '../lib/seat'
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PosterArt from '../components/PosterArt.vue'
import TicketModal from '../components/TicketModal.vue'
import Icon from '../components/Icon.vue'
import { api, downloadFile } from '../lib/api'
import { toast } from '../lib/ui'
import { money, num, fmtDate, fmtTime, fmtFull, fmtCode } from '../lib/format'

const route = useRoute()
const router = useRouter()
const list = ref(null)
const error = ref('')
const open = ref(null)
const busyId = ref(null)

// شمارش معکوس مهلت پرداخت (اختلاف ساعت سرور و دستگاه با serverNow خنثی می‌شود)
const now = ref(Date.now())
const tick = setInterval(() => (now.value = Date.now()), 1000)
onBeforeUnmount(() => clearInterval(tick))
const loadedAt = Date.now()
const secsLeft = (b) => (b.payDeadline ? Math.max(0, Math.floor((new Date(b.payDeadline) - new Date(b.serverNow) - (now.value - loadedAt)) / 1000)) : null)
const mmss = (s) => `${num(Math.floor(s / 60))}:${num(Math.floor((s % 60) / 10))}${num(s % 10)}`
let reloaded = false
watch(() => (list.value ?? []).some((b) => b.status === 'PendingPayment' && secsLeft(b) === 0), async (expired) => {
  if (expired && !reloaded) { reloaded = true; try { list.value = await api.get('/api/bookings/mine') } catch {} }
})

onMounted(async () => {
  try {
    list.value = await api.get('/api/bookings/mine')
    // لینک «نمایش بلیت» ربات: /my-bookings?ticket=12
    const t = Number(route.query.ticket)
    if (t) {
      const b = list.value.find((x) => x.id === t && x.status === 'Confirmed')
      if (b) open.value = withSeats(b)
      router.replace({ query: {} })
    }
  } catch (e) { error.value = e.message }
})

const STATUS = {
  PendingPayment: { label: 'منتظر پرداخت', cls: 'warn' },
  AwaitingReview: { label: 'در انتظار تأیید', cls: 'info' },
  Confirmed: { label: 'پرداخت‌شده', cls: 'ok' },
  Rejected: { label: 'ردشده', cls: 'bad' },
  Expired: { label: 'منقضی', cls: 'mute' },
  Cancelled: { label: 'لغوشده', cls: 'mute' },
}
const isPast = (b) => new Date(b.startsAt) <= new Date()
const seatsOf = (b) => (b.seats?.length ? b.seats.map((s) => `${s.row}${s.number}`) : (b.seatsText || '').split(',').filter(Boolean))
const withSeats = (b) => ({ ...b, seats: b.seats?.length ? b.seats : seatsOf(b).map((x) => ({ row: x.replace(/\d+$/, ''), number: Number(x.match(/\d+$/)?.[0]) })) })

const groups = computed(() => {
  const all = list.value ?? []
  return [
    { title: 'نیاز به اقدام', items: all.filter((b) => b.status === 'PendingPayment' || b.status === 'AwaitingReview') },
    { title: 'پیش رو', items: all.filter((b) => b.status === 'Confirmed' && !isPast(b)).sort((a, b) => a.startsAt.localeCompare(b.startsAt)) },
    { title: 'گذشته و ناموفق', items: all.filter((b) => (b.status === 'Confirmed' && isPast(b)) || ['Rejected', 'Expired', 'Cancelled'].includes(b.status)) },
  ].filter((g) => g.items.length)
})

async function pdf(b) {
  busyId.value = b.id
  try { await downloadFile(`/api/bookings/${b.id}/pdf`, `ticket-${b.code}.pdf`) } catch (e) { toast(e.message, 'error') } finally { busyId.value = null }
}
</script>

<template>
  <div class="row between" style="margin-bottom: 18px">
    <h1 style="margin: 0">رزروهای من</h1>
    <span v-if="list?.length" class="muted small">{{ num(list.length) }} رزرو</span>
  </div>

  <div v-if="error" class="alert error" style="margin-bottom: 12px">{{ error }}</div>
  <Skeleton v-if="!list && !error" variant="tickets" />
  <div v-else-if="list && !list.length" class="empty card">
    <Icon name="ticket" :size="32" />
    <p style="margin: 8px 0 12px">هنوز رزروی نداری.</p>
    <RouterLink class="btn" to="/">انتخاب فیلم</RouterLink>
  </div>

  <template v-for="g in groups" :key="g.title">
    <h2 class="gtitle">{{ g.title }} <span class="muted small">({{ num(g.items.length) }})</span></h2>
    <div class="list">
      <article v-for="b in g.items" :key="b.id" class="tk" :class="{ dim: ['Rejected', 'Expired', 'Cancelled'].includes(b.status) || (b.status === 'Confirmed' && isPast(b)) }">
        <div class="main">
          <div class="thumb"><PosterArt :movie="{ title: b.movie, genre: b.genre, posterUrl: b.posterUrl }" :show-title="false" /></div>
          <div class="info">
            <div class="row" style="gap: 8px">
              <h3>{{ b.movie }}</h3>
              <span class="st" :class="STATUS[b.status].cls">{{ b.checkedInAt ? 'استفاده شده' : STATUS[b.status].label }}</span>
            </div>
            <div class="muted small line"><Icon name="film" :size="14" /> {{ b.cinema }} · {{ b.hall }}</div>
            <div class="when"><Icon name="calendar" :size="16" /> {{ fmtDate(b.startsAt) }} <b>{{ fmtTime(b.startsAt) }}</b></div>
            <div class="seats"><span v-for="l in seatLines(seatsOf(b))" :key="l" class="chip">{{ l }}</span></div>
            <div class="muted small">ثبت‌شده در {{ fmtFull(b.createdAt) }} · <b class="amount">{{ money(b.totalPrice) }}</b></div>
            <div v-if="b.status === 'Rejected' && b.rejectReason" class="reason small"><Icon name="alert" :size="13" /> {{ b.rejectReason }}</div>
          </div>
        </div>

        <div class="stub">
          <template v-if="b.status === 'Confirmed'">
            <div class="codebox"><span class="muted small">کد بلیت</span><b dir="ltr">{{ fmtCode(b.code) }}</b></div>
            <button class="btn sm" @click="open = withSeats(b)"><Icon name="qr" :size="16" /> {{ isPast(b) || b.checkedInAt ? 'مشاهدهٔ بلیت' : 'نمایش بلیت' }}</button>
            <button class="btn secondary sm" :disabled="busyId === b.id" @click="pdf(b)"><Icon name="download" :size="16" /> {{ busyId === b.id ? 'در حال آماده‌سازی…' : 'دانلود PDF' }}</button>
          </template>
          <template v-else-if="b.status === 'PendingPayment'">
            <div class="codebox"><span class="muted small">مبلغ قابل پرداخت</span><b class="price">{{ money(b.totalPrice) }}</b></div>
            <div v-if="secsLeft(b) !== null" class="timer" :class="{ low: secsLeft(b) < 120 }" role="timer"><Icon name="clock" :size="14" /> مهلت پرداخت <b>{{ mmss(secsLeft(b)) }}</b></div>
            <RouterLink class="btn sm" :to="{ name: 'pay', params: { id: b.id } }"><Icon name="card" :size="16" /> ادامهٔ پرداخت</RouterLink>
          </template>
          <template v-else-if="b.status === 'AwaitingReview'">
            <div class="codebox"><span class="muted small">رسید ثبت شد</span><b>در حال بررسی</b></div>
            <RouterLink class="btn secondary sm" :to="{ name: 'pay', params: { id: b.id } }">پیگیری وضعیت</RouterLink>
          </template>
          <template v-else>
            <div class="codebox"><span class="muted small">وضعیت</span><b>{{ STATUS[b.status].label }}</b></div>
            <RouterLink class="btn secondary sm" :to="{ name: 'movie', params: { id: b.movieId } }">رزرو دوباره</RouterLink>
          </template>
        </div>
      </article>
    </div>
  </template>

  <TicketModal v-if="open" :booking="open" @close="open = null" />
</template>

<style scoped>
.gtitle { margin: 28px 0 12px; font-size: 1.05rem; }
.list { display: grid; gap: 14px; }
.tk { display: grid; grid-template-columns: minmax(0, 1fr) 210px; background: var(--surface); border: 1px solid var(--border); border-radius: 14px; overflow: hidden; }
.tk.dim { opacity: .75; }
.main { display: flex; gap: 16px; padding: 18px; }
.thumb { width: 78px; flex: none; align-self: flex-start; border-radius: 8px; overflow: hidden; border: 1px solid var(--border); }
.info { display: grid; gap: 6px; align-content: start; min-width: 0; }
.info h3 { margin: 0; font-size: 1.1rem; }
.line { display: flex; align-items: center; gap: 6px; }
.when { display: flex; align-items: center; gap: 8px; color: var(--text); }
.when b { color: var(--sky); font-weight: 700; }
.seats { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; border-radius: 6px; padding: 0 10px; font-weight: 700; }
.amount { color: var(--text); font-weight: 500; }
.reason { display: flex; align-items: center; gap: 5px; color: #fca5a5; }

.st { padding: 0 11px; border-radius: 999px; font-size: .75rem; border: 1px solid; white-space: nowrap; }
.st.ok { background: #0f2619; color: #86efac; border-color: #1d4a2e; }
.st.warn { background: #33270e; color: #f0b952; border-color: #5a4514; }
.st.info { background: var(--primary-soft); color: #9cc2ff; border-color: #1f3a68; }
.st.bad { background: #2a1417; color: #fca5a5; border-color: #4a2428; }
.st.mute { background: var(--surface-2); color: var(--muted); border-color: var(--border); }

.stub { position: relative; display: grid; gap: 10px; align-content: center; padding: 18px; border-inline-start: 1px dashed var(--border); background: var(--surface-2); }
.stub::before, .stub::after { content: ''; position: absolute; inset-inline-end: calc(100% - 8px); width: 18px; height: 18px; border-radius: 50%; background: var(--bg); border: 1px solid var(--border); }
.stub::before { top: -10px; } .stub::after { bottom: -10px; }
.codebox { display: grid; text-align: center; line-height: 1.4; margin-bottom: 4px; }
.codebox b { font-family: ui-monospace, 'SF Mono', Menlo, monospace; font-size: 1.05rem; letter-spacing: .06em; }
.stub .btn { width: 100%; }

@media (max-width: 720px) {
  .tk { grid-template-columns: 1fr; }
  .stub { border-inline-start: 0; border-top: 1px dashed var(--border); grid-template-columns: 1fr 1fr; }
  .stub::before, .stub::after { display: none; }
  .codebox { grid-column: 1 / -1; }
  .main { padding: 14px; gap: 12px; }
  .thumb { width: 60px; }
}
.codebox b.price { font-family: inherit; letter-spacing: 0; font-size: 1.1rem; font-weight: 700; }
.timer { display: flex; align-items: center; justify-content: center; gap: 6px; padding: 5px 10px; border-radius: 10px; font-size: .8rem; background: #261e0b; border: 1px solid #5a4514; color: #e6c67a; }
.timer b { font-variant-numeric: tabular-nums; direction: ltr; font-size: .95rem; color: #f5d98a; }
.timer.low { background: #2a1417; border-color: #6b2a30; color: #fca5a5; }
.timer.low b { color: #fecaca; }
</style>
