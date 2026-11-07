<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { seatLines } from '../../lib/seat'
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import Icon from '../../components/Icon.vue'
import { api } from '../../lib/api'
import { toast } from '../../lib/ui'
import { num, money, fmtDate, fmtTime, fmtFull, fmtCode } from '../../lib/format'

const route = useRoute()
const STATUS = {
  valid: { label: 'معتبر', cls: 'ok' },
  used: { label: 'وارد شده', cls: '' },
  expired: { label: 'منقضی', cls: 'warn' },
  unpaid: { label: 'پرداخت‌نشده', cls: 'warn' },
}

const fromQuery = (q) => ({
  q: q.q ?? '', movieId: q.movieId ?? '', cinemaId: q.cinemaId ?? '', showtimeId: q.showtimeId ?? '', status: q.status ?? 'all', pay: q.pay ?? 'confirmed',
})
const f = reactive({ ...fromQuery(route.query), page: 1 })
const data = ref(null)
const movies = ref([])
const cinemas = ref([])
const loading = ref(false)

async function load() {
  loading.value = true
  const p = new URLSearchParams({ page: f.page, status: f.status, pay: f.pay })
  for (const k of ['q', 'movieId', 'cinemaId', 'showtimeId']) if (f[k]) p.set(k, f[k])
  try { data.value = await api.get(`/api/admin/bookings?${p}`) }
  catch (e) { toast(e.message, 'error') } finally { loading.value = false }
}

onMounted(async () => {
  try { ;[movies.value, cinemas.value] = await Promise.all([api.get('/api/admin/movies'), api.get('/api/admin/cinemas')]) } catch { /* فیلترها بدون لیست هم کار می‌کنند */ }
  load()
})

// تغییر فیلتر → برگشت به صفحهٔ ۱ (جست‌وجوی متنی با تأخیر کوتاه)
let timer
watch(() => [f.q, f.movieId, f.cinemaId, f.showtimeId, f.status, f.pay], () => {
  clearTimeout(timer)
  timer = setTimeout(() => { f.page = 1; load() }, 250)
})
watch(() => f.page, load)

// لینک‌های بیرونی (مثلاً از صفحهٔ فیلم‌ها یا سانس‌ها) فیلترها را از نو تنظیم می‌کنند
watch(() => route.query, (q) => Object.assign(f, fromQuery(q), { page: 1 }))

const filtered = computed(() => f.q || f.movieId || f.cinemaId || f.showtimeId || f.status !== 'all' || f.pay !== 'confirmed')
const reset = () => Object.assign(f, { q: '', movieId: '', cinemaId: '', showtimeId: '', status: 'all', pay: 'confirmed', page: 1 })
</script>

<template>
  <div class="page-head">
    <div><h1>بلیت‌های فروخته‌شده</h1><p class="muted">همهٔ رزروها؛ بر اساس فیلم، سینما، سانس، خریدار یا وضعیت ورود فیلتر کنید.</p></div>
    <RouterLink class="btn" :to="{ name: 'admin-tickets' }"><Icon name="qr" :size="17" /> بررسی بلیت</RouterLink>
  </div>

  <div v-if="data" class="stats">
    <div class="panel stat"><span class="muted small">رزرو</span><b>{{ num(data.summary.bookings) }}</b></div>
    <div class="panel stat"><span class="muted small">صندلی فروخته‌شده</span><b>{{ num(data.summary.seats) }}</b></div>
    <div class="panel stat"><span class="muted small">وارد شده</span><b>{{ num(data.summary.checkedIn) }}</b></div>
    <div class="panel stat"><span class="muted small">مبلغ فروش</span><b class="money">{{ money(data.summary.revenue) }}</b></div>
  </div>

  <div class="filters">
    <div class="search">
      <Icon name="search" :size="17" />
      <input v-model="f.q" placeholder="نام خریدار، ایمیل یا کد بلیت…" aria-label="جست‌وجو" />
    </div>
    <select v-model="f.movieId" aria-label="فیلم"><option value="">همهٔ فیلم‌ها</option><option v-for="m in movies" :key="m.id" :value="m.id">{{ m.title }}</option></select>
    <select v-model="f.cinemaId" aria-label="سینما"><option value="">همهٔ سینماها</option><option v-for="c in cinemas" :key="c.id" :value="c.id">{{ c.name }}</option></select>
    <select v-model="f.pay" aria-label="وضعیت پرداخت">
      <option value="confirmed">پرداخت تأییدشده</option>
      <option value="awaiting">در انتظار تأیید مدیر</option>
      <option value="pending">منتظر پرداخت کاربر</option>
      <option value="rejected">ردشده</option>
      <option value="expired">منقضی</option>
      <option value="cancelled">لغوشده</option>
      <option value="all">همهٔ رزروها</option>
    </select>
    <select v-model="f.status" aria-label="وضعیت">
      <option value="all">همهٔ وضعیت‌ها</option>
      <option value="valid">معتبر (ورود نشده)</option>
      <option value="used">وارد شده</option>
      <option value="expired">منقضی</option>
    </select>
    <button v-if="filtered" class="btn secondary sm" @click="reset">پاک‌کردن فیلترها</button>
  </div>

  <div v-if="f.showtimeId && data?.showtime" class="chip-filter">
    <span>سانس: <b>{{ data.showtime.movie }}</b> · {{ fmtDate(data.showtime.startsAt) }} {{ fmtTime(data.showtime.startsAt) }} · {{ data.showtime.cinema }} / {{ data.showtime.hall }}</span>
    <button class="iconbtn" aria-label="حذف فیلتر سانس" @click="f.showtimeId = ''"><Icon name="x" :size="15" /></button>
  </div>

  <Skeleton v-if="!data" variant="table" />
  <div v-else-if="!data.items.length" class="panel empty">
    <p>{{ filtered ? 'بلیتی با این فیلتر پیدا نشد.' : 'هنوز بلیتی فروخته نشده است.' }}</p>
    <button v-if="filtered" class="btn secondary" @click="reset">پاک‌کردن فیلترها</button>
  </div>

  <section v-else class="panel table-wrap" :class="{ busy: loading }">
    <table>
      <thead>
        <tr><th>بلیت</th><th>فیلم و سانس</th><th>صندلی‌ها</th><th>خریدار</th><th>مبلغ</th><th>وضعیت</th><th /></tr>
      </thead>
      <tbody>
        <tr v-for="b in data.items" :key="b.id">
          <td class="nw c-code"><b class="code" dir="ltr">{{ fmtCode(b.code) }}</b><div class="muted small">{{ fmtFull(b.createdAt) }}</div></td>
          <td class="c-movie">
            <b>{{ b.movie }}</b>
            <div><button class="link" title="فقط بلیت‌های این سانس" @click="f.showtimeId = b.showtimeId">{{ fmtDate(b.startsAt) }} · {{ fmtTime(b.startsAt) }}</button></div>
            <div class="muted small">{{ b.cinema }} · {{ b.hall }}</div>
          </td>
          <td class="c-seats"><span class="seats"><span v-for="l in seatLines(b.seats.length ? b.seats : b.seatsText.split(',').filter(Boolean))" :key="l" class="chip">{{ l }}</span></span></td>
          <td class="c-buyer">{{ b.buyer }}<div class="muted small" dir="ltr" style="text-align: start">{{ b.email }}</div></td>
          <td class="nw c-amount">{{ money(b.totalPrice) }}</td>
          <td class="nw c-status">
            <span class="badge" :class="STATUS[b.status]?.cls">{{ STATUS[b.status]?.label }}</span>
            <div v-if="b.checkedInAt" class="muted small">{{ fmtTime(b.checkedInAt) }}</div>
          </td>
          <td class="c-act"><RouterLink class="btn secondary sm" :to="{ name: 'admin-tickets', query: { code: b.code } }">بررسی</RouterLink></td>
        </tr>
      </tbody>
    </table>
  </section>

  <nav v-if="data && data.pages > 1" class="pager" aria-label="صفحه‌بندی">
    <button class="btn secondary sm" :disabled="f.page <= 1" @click="f.page--">قبلی</button>
    <span class="muted small">صفحهٔ {{ num(data.page) }} از {{ num(data.pages) }} · {{ num(data.total) }} بلیت</span>
    <button class="btn secondary sm" :disabled="f.page >= data.pages" @click="f.page++">بعدی</button>
  </nav>
</template>

<style scoped>
.stats { display: grid; gap: 12px; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); margin-bottom: 18px; }
.stat { padding: 14px 16px; display: grid; gap: 0; }
.stat b { font-size: 1.4rem; line-height: 1.4; }
.stat b.money { font-size: 1.05rem; padding-top: 6px; }

.filters { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; margin-bottom: 14px; }
.filters select { width: auto; min-width: 150px; }
.search { display: flex; align-items: center; gap: 8px; flex: 1; min-width: 240px; color: var(--muted); background: var(--surface-2); border: 1px solid var(--border); border-radius: 10px; padding: 0 12px; }
.search input { border: 0; padding: 8px 0; background: none; box-shadow: none !important; }
.search:focus-within { border-color: var(--primary); }

.chip-filter { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 8px 12px; margin-bottom: 14px; border-radius: 10px; background: var(--primary-soft); border: 1px solid #1f3a68; color: #9cc2ff; font-size: .9rem; }
.chip-filter b { color: var(--text); }

.panel.busy { opacity: .6; transition: opacity .15s; }
.code { font-family: ui-monospace, 'SF Mono', Menlo, monospace; letter-spacing: .06em; }
td { white-space: normal; vertical-align: top; }
td.nw { white-space: nowrap; }
.seats { display: flex; flex-wrap: wrap; gap: 4px; min-width: 100px; max-width: 150px; }
.chip { background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; border-radius: 6px; padding: 0 8px; font-weight: 700; font-size: .8rem; }
.link { background: none; border: 0; padding: 0; font: inherit; font-size: .85rem; color: var(--sky); cursor: pointer; }
.link:hover { text-decoration: underline; }
.empty { padding: 36px; }
.empty p { margin-bottom: 12px; }
.pager { display: flex; align-items: center; justify-content: center; gap: 14px; margin-top: 16px; }

/* موبایل: هر بلیت یک کارت (بدون جدول و اسکرول افقی) */
@media (max-width: 720px) {
  .table-wrap { background: none; border: 0; overflow: visible; }
  table, tbody { display: block; }
  thead { display: none; }
  tr { display: grid; grid-template-columns: minmax(0, 1fr) auto; grid-template-areas: 'code status' 'movie movie' 'seats seats' 'buyer amount' 'act act'; gap: 8px 12px; padding: 14px; margin-bottom: 12px; background: var(--surface); border: 1px solid var(--border); border-radius: 14px; }
  td { display: block; padding: 0; border: 0; white-space: normal; min-width: 0; }
  .c-code { grid-area: code; } .c-status { grid-area: status; text-align: end; }
  .c-movie { grid-area: movie; padding-top: 10px !important; border-top: 1px dashed var(--border) !important; }
  .c-seats { grid-area: seats; } .c-buyer { grid-area: buyer; } .c-amount { grid-area: amount; align-self: end; font-weight: 700; }
  .c-act { grid-area: act; } .c-act .btn { width: 100%; justify-content: center; }
  .seats { max-width: none; }
  .filters select, .search { min-width: 0; flex: 1 1 100%; }
  .filters select { width: 100%; }
}
</style>
