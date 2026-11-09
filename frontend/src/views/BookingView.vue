<script setup>
import { runBusy } from '../lib/busy'
import Skeleton from '../components/Skeleton.vue'
import { seatOne } from '../lib/seat'
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import SeatMap from '../components/SeatMap.vue'
import PosterArt from '../components/PosterArt.vue'
import Icon from '../components/Icon.vue'
import NoteChip from '../components/NoteChip.vue'
import { api } from '../lib/api'
import * as tg from '../lib/tg'
import { auth } from '../lib/auth'
import { money, num, fmtFull, fmtCode } from '../lib/format'

const MAX_SEATS = 8
const POLL_MS = 8000
const HOLD_SECONDS = 300 // باید با HoldsController.Ttl برابر باشد
const props = defineProps({ id: String })
const router = useRouter()

const st = ref(null)
const selected = ref([]) // کلیدهای "rowId:number"
const error = ref('')
const notice = ref('')
const busy = ref(false)
const holdUntil = ref(null) // زمان پایان نگه‌داری صندلی‌ها (ms)
const now = ref(Date.now())

const labelOf = (key) => {
  const [rid, n] = key.split(':').map(Number)
  return `${st.value.rows.find((r) => r.id === rid)?.label}-${n}`
}

// وضعیت سانس را از سرور می‌گیرد؛ هر چند ثانیه هم تکرار می‌شود تا رزرو و نگه‌داری دیگران زنده دیده شود.
async function load(initial = false) {
  try {
    const d = await api.get(`/api/showtimes/${props.id}`)
    st.value = d
    if (initial) {
      selected.value = d.rows.flatMap((r) => r.mine.map((n) => `${r.id}:${n}`))
      holdUntil.value = d.holdExpiresAt ? Date.parse(d.holdExpiresAt) : null
    }
    const blocked = new Set(d.rows.flatMap((r) => [...r.taken, ...r.disabled, ...r.held].map((n) => `${r.id}:${n}`)))
    const lost = selected.value.filter((k) => blocked.has(k))
    if (lost.length) {
      selected.value = selected.value.filter((k) => !blocked.has(k))
      notice.value = `صندلی ${lost.map(labelOf).join('، ')} دیگر در دسترس نیست.`
    }
  } catch (e) {
    if (!st.value) error.value = e.message
  }
}

// ---------- نگه‌داری موقت صندلی‌ها ----------
let syncTimer
const scheduleSync = () => {
  if (!auth.token) return
  clearTimeout(syncTimer)
  syncTimer = setTimeout(sync, 250)
}
async function sync() {
  try {
    const res = await api.put(`/api/showtimes/${props.id}/holds`, { seats: seatsPayload() })
    holdUntil.value = res.expiresAt ? Date.parse(res.expiresAt) : null
    if (res.unavailable.length) {
      const bad = new Set(res.unavailable.map((u) => `${u.rowId}:${u.number}`))
      notice.value = `صندلی ${[...bad].map(labelOf).join('، ')} هم‌اکنون در دسترس نیست.`
      selected.value = selected.value.filter((k) => !bad.has(k))
      await load()
    }
  } catch (e) {
    if (e.status === 409) { await load(); scheduleSync() } else error.value = e.message
  }
}
const seatsPayload = () => selected.value.map((k) => { const [rowId, number] = k.split(':').map(Number); return { rowId, number } })

function toggle(row, n) {
  notice.value = ''
  tg.haptic.select()
  const key = `${row.id}:${n}`
  const i = selected.value.indexOf(key)
  if (i >= 0) selected.value.splice(i, 1)
  else if (selected.value.length >= MAX_SEATS) { notice.value = `حداکثر ${num(MAX_SEATS)} صندلی در هر رزرو قابل انتخاب است.`; return }
  else selected.value.push(key)
  scheduleSync()
}

// حذف صندلی از فهرست «بلیت شما»: هم از انتخاب درمی‌آید هم روی نقشه آزاد می‌شود
function unselect(key) {
  const i = selected.value.indexOf(key)
  if (i < 0) return
  tg.haptic.select()
  selected.value.splice(i, 1)
  notice.value = ''
  scheduleSync()
}

// ---------- تایمر ----------
const remaining = computed(() => (holdUntil.value && selected.value.length ? Math.max(0, Math.floor((holdUntil.value - now.value) / 1000)) : null))
const mmss = computed(() => {
  const s = remaining.value ?? 0
  return `${num(Math.floor(s / 60))}:${num(Math.floor((s % 60) / 10))}${num(s % 10)}`
})
let tick, poll
onMounted(() => {
  load(true)
  tick = setInterval(() => {
    now.value = Date.now()
    if (remaining.value === 0) {
      selected.value = []
      holdUntil.value = null
      notice.value = 'زمان نگه‌داری صندلی‌ها تموم شد؛ دوباره انتخاب کن.'
      load()
    }
  }, 1000)
  poll = setInterval(() => { if (!document.hidden) load() }, POLL_MS)
})
onBeforeUnmount(() => {
  clearInterval(tick); clearInterval(poll); clearTimeout(syncTimer)
  if (auth.token && selected.value.length && !busy.value) api.put(`/api/showtimes/${props.id}/holds`, { seats: [] }).catch(() => {})
})

const items = computed(() => {
  if (!st.value) return []
  const byId = Object.fromEntries(st.value.rows.map((r) => [r.id, r]))
  return selected.value
    .map((k) => {
      const [rid, n] = k.split(':').map(Number)
      const r = byId[rid]
      return { key: k, rowId: rid, number: n, label: `${r.label}:${n}`, price: r.price, order: st.value.rows.indexOf(r) }
    })
    .sort((a, b) => a.order - b.order || a.number - b.number)
})
const total = computed(() => items.value.reduce((s, x) => s + x.price, 0))

// رزرو «منتظر پرداخت» ساخته می‌شود و صندلی‌ها تا پایان مهلت پرداخت اشغال می‌مانند؛ بلیت بعد از تأیید پرداخت صادر می‌شود
async function submit() {
  if (!items.value.length || busy.value) return
  busy.value = true
  error.value = ''
  const done = await runBusy('در حال نگه‌داشتن صندلی‌ها…', async (step) => {
    try {
      const res = await api.post('/api/bookings', {
        showtimeId: st.value.id,
        seats: items.value.map((x) => ({ rowId: x.rowId, number: x.number })),
      })
      selected.value = []
      holdUntil.value = null
      tg.haptic.success()
      step('رزرو ثبت شد ✓', 'در حال رفتن به صفحهٔ پرداخت…')
      await router.push({ name: 'pay', params: { id: res.id } })
      return true
    } catch (e) {
      error.value = e.message
      tg.haptic.error()
      if (e.status === 409) await load()
      return false
    }
  }, 'چند لحظه صبر کن')
  if (done !== true) busy.value = false
}

// دکمهٔ اصلی بومی تلگرام (پایین صفحه)؛ بیرون از تلگرام همان دکمهٔ داخل صفحه نمایش داده می‌شود
watch([items, busy, total], () => {
  if (!tg.mainButton.supported) return
  items.value.length ? tg.mainButton.show({ text: `ادامه و پرداخت · ${money(total.value)}`, onClick: submit, loading: busy.value }) : tg.mainButton.hide()
})
onBeforeUnmount(() => tg.mainButton.hide())
</script>

<template>
  <div v-if="!st && error" class="alert error">{{ error }}</div>
  <Skeleton v-else-if="!st" variant="seats" />

  <template v-else>
    <section class="hero-band head">
      <div class="art"><PosterArt :movie="{ title: st.movie.title, genre: '' }" :show-title="false" /></div>
      <div class="info">
        <div class="steps"><span class="on">۱ · انتخاب صندلی</span><span>۲ · تأیید رزرو</span></div>
        <h1>{{ st.movie.title }}</h1>
        <div v-if="st.movie.specialNote"><NoteChip :note="st.movie.specialNote" size="sm" /></div>
        <div class="meta">
          <span><Icon name="film" :size="16" /> {{ st.cinema.name }}</span><span><Icon name="seat" :size="16" /> {{ st.hall.name }}</span><span><Icon name="calendar" :size="16" /> {{ fmtFull(st.startsAt) }}</span>
        </div>
      </div>
    </section>


    <div class="layout">
      <section class="card map">
        <SeatMap :rows="st.rows" mode="select" zoomable :selected="selected" @toggle="toggle" />
      </section>

      <aside class="ticket">
        <div class="thead">
          <h3><Icon name="ticket" :size="18" /> بلیت تو</h3>
          <span class="small">{{ st.movie.title }}</span>
        </div>
        <div class="tbody">
          <p v-if="!items.length" class="muted">صندلی‌هات رو روی نقشه انتخاب کن.</p>
          <ul v-else>
            <li v-for="x in items" :key="x.key">
              <span class="seatchip">{{ seatOne(x.label) }}</span>
              <span class="pr">{{ money(x.price) }}</span>
              <button type="button" class="rm" :aria-label="`حذف ${seatOne(x.label)}`" @click="unselect(x.key)"><Icon name="x" :size="14" /></button>
            </li>
          </ul>
        </div>
        <div class="perf" aria-hidden="true" />
        <div class="tfoot">
          <div class="total"><span>جمع کل</span><strong>{{ money(total) }}</strong></div>
          <div v-if="remaining !== null" class="hold" :class="{ low: remaining < 60 }" role="timer">
            <div class="top">
              <span class="lbl"><Icon name="clock" :size="16" /> زمان باقی‌مانده</span>
              <b>{{ mmss }}</b>
            </div>
            <div class="bar"><i :style="{ width: `${Math.min(100, (remaining / HOLD_SECONDS) * 100)}%` }" /></div>
            <p>{{ remaining < 60 ? 'کمتر از یک دقیقه مانده؛ هرچه زودتر ادامه دهید.' : 'اگر تا پایان زمان ادامه ندهید، صندلی‌ها آزاد می‌شوند.' }}</p>
          </div>
          <div v-if="notice" class="alert info small">{{ notice }}</div>
          <div v-if="error" class="alert error small">{{ error }}</div>
          <button v-if="!tg.mainButton.supported" class="btn lg" style="width: 100%" :disabled="!items.length || busy" @click="submit">
            {{ busy ? 'در حال ثبت…' : 'ادامه و پرداخت' }}
          </button>
          <p class="muted small" style="margin: 0"><Icon name="card" :size="14" /> پرداخت کارت‌به‌کارت است؛ بلیت پس از تأیید پرداخت صادر می‌شود.</p>
        </div>
      </aside>
    </div>
  </template>

</template>

<style scoped>
.head { display: grid; grid-template-columns: 76px 1fr; gap: 20px; align-items: center; padding: 18px 24px; }
.art { border-radius: 8px; overflow: hidden; border: 1px solid var(--border); }
.info { display: grid; gap: 6px; }
.info h1 { margin: 0; font-size: 1.6rem; }
.meta { display: flex; gap: 18px; flex-wrap: wrap; color: var(--muted); font-size: .92rem; }
.meta span { display: inline-flex; align-items: center; gap: 6px; }
.steps { display: flex; gap: 8px; font-size: .78rem; }
.steps span { color: var(--muted); }
.steps span.on { color: var(--sky); font-weight: 700; }

.layout { display: grid; grid-template-columns: minmax(0, 1fr) 320px; gap: 18px; align-items: start; margin-top: 18px; }
.map { padding: 28px 16px; }

.ticket { position: sticky; top: 76px; background: var(--surface); border-radius: 12px; border: 1px solid var(--border); overflow: hidden; }
.thead { padding: 14px 18px; display: grid; border-bottom: 1px solid var(--border); }
.thead h3 { margin: 0; display: flex; align-items: center; gap: 8px; }
.thead .small { color: var(--muted); }
.tbody { padding: 14px 18px; min-height: 84px; }
.tbody p { margin: 0; }
.tbody ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.tbody li { display: flex; align-items: center; gap: 8px; font-size: .92rem; }
.tbody li .pr { margin-inline-start: auto; }
.rm { display: grid; place-items: center; width: 28px; height: 28px; padding: 0; border-radius: 50%; border: 1px solid var(--border); background: transparent; color: var(--muted); cursor: pointer; }
.rm:hover, .rm:active { background: #2a1417; border-color: #6b2a30; color: #fca5a5; }
.seatchip { background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; border-radius: 6px; padding: 0 10px; font-weight: 700; }
.perf { position: relative; border-top: 1px dashed var(--border); }
.perf::before, .perf::after { content: ''; position: absolute; top: -9px; width: 18px; height: 18px; border-radius: 50%; background: var(--bg); border: 1px solid var(--border); }
.perf::before { inset-inline-start: -10px; } .perf::after { inset-inline-end: -10px; }
.tfoot { padding: 16px 18px 18px; display: grid; gap: 12px; }
.hold { display: grid; gap: 8px; padding: 12px 14px; border-radius: 10px; background: #261e0b; border: 1px solid #5a4514; color: #e6c67a; }
.hold .top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.hold .lbl { display: inline-flex; align-items: center; gap: 6px; font-weight: 500; }
.hold b { font-size: 1.4rem; line-height: 1; color: #f5d98a; font-variant-numeric: tabular-nums; direction: ltr; }
.hold .bar { height: 4px; border-radius: 2px; background: rgb(240 185 82 / 18%); overflow: hidden; }
.hold .bar i { display: block; height: 100%; background: #f0b952; transition: width 1s linear; }
.hold p { margin: 0; font-size: .8rem; color: #b9a269; }
.hold.low { background: #2a1417; border-color: #6b2a30; color: #fca5a5; }
.hold.low b { color: #fecaca; }
.hold.low .bar { background: rgb(248 113 113 / 20%); }
.hold.low .bar i { background: #f87171; }
.hold.low p { color: #c98a8a; }
.total { display: flex; justify-content: space-between; align-items: baseline; }
.total strong { font-size: 1.2rem; color: var(--text); }

@media (max-width: 900px) {
  .layout { grid-template-columns: minmax(0, 1fr); }
  .ticket { position: static; }
  .head { padding: 14px; grid-template-columns: 56px 1fr; gap: 14px; }
  .info h1 { font-size: 1.25rem; }
}
</style>
