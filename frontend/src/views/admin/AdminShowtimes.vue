<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Modal from '../../components/Modal.vue'
import Icon from '../../components/Icon.vue'
import JalaliDateTime from '../../components/JalaliDateTime.vue'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { num, fmtDate, fmtTime, dayKey, toInputValue } from '../../lib/format'

const PRESETS = [
  { f: 0.8, label: 'تخفیف', hint: '۰٫۸×' },
  { f: 1, label: 'عادی', hint: '۱×' },
  { f: 1.2, label: 'شب', hint: '۱٫۲×' },
  { f: 1.3, label: 'تعطیل', hint: '۱٫۳×' },
]

const showtimes = ref(null)
const movies = ref([])
const cinemas = ref([])
const route = useRoute()
const router = useRouter()
// فیلتر اولیه از لینک (مثلاً از صفحهٔ فیلم‌ها): ?movieId=3&new=1
const filter = ref({ cinemaId: route.query.cinemaId ?? '', movieId: route.query.movieId ?? '', past: false })
const form = ref(null)
const saving = ref(false)

async function loadList() {
  const p = new URLSearchParams()
  if (filter.value.cinemaId) p.set('cinemaId', filter.value.cinemaId)
  if (filter.value.movieId) p.set('movieId', filter.value.movieId)
  if (filter.value.past) p.set('upcomingOnly', 'false')
  try { showtimes.value = await api.get(`/api/admin/showtimes?${p}`) } catch (e) { toast(e.message, 'error') }
}
onMounted(async () => {
  try { ;[movies.value, cinemas.value] = await Promise.all([api.get('/api/admin/movies'), api.get('/api/admin/cinemas')]) }
  catch (e) { toast(e.message, 'error') }
  await loadList()
  if (route.query.new && canCreate.value) {
    startNew()
    if (route.query.movieId) form.value.movieId = Number(route.query.movieId)
  }
  if (route.query.new) router.replace({ query: { ...route.query, new: undefined } })
})
// لینک تازه هنگامی که همین صفحه باز است (مثلاً از منو)
watch(() => route.query, (q) => { filter.value.movieId = q.movieId ?? ''; filter.value.cinemaId = q.cinemaId ?? '' })
watch(filter, loadList, { deep: true })

const halls = computed(() => cinemas.value.flatMap((c) => c.halls.map((h) => ({ ...h, cinema: c.name }))))
const canCreate = computed(() => movies.value.length && halls.value.some((h) => h.rowCount))

// گروه‌بندی بر اساس روز
const days = computed(() => {
  const m = new Map()
  for (const s of showtimes.value ?? []) {
    const k = dayKey(s.startsAt)
    if (!m.has(k)) m.set(k, { key: k, label: fmtDate(s.startsAt), items: [] })
    m.get(k).items.push(s)
  }
  return [...m.values()]
})

const pad = (n) => String(n).padStart(2, '0')
const addDays = (local, d) => {
  const dt = new Date(local)
  dt.setDate(dt.getDate() + d)
  return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`
}

function startNew() {
  const first = halls.value.find((h) => h.rowCount)
  form.value = { id: null, movieId: movies.value[0]?.id ?? '', hallId: first?.id ?? '', startsAt: '', priceFactor: 1, repeatDays: 1 }
}
const startEdit = (s) => (form.value = { id: s.id, movieId: s.movieId, hallId: s.hallId, startsAt: toInputValue(s.startsAt), priceFactor: s.priceFactor, repeatDays: 1 })

// ساعت پایان تقریبی (مدت فیلم + ۱۵ دقیقه پاک‌سازی)
const endsAt = computed(() => {
  const f = form.value
  const m = movies.value.find((x) => x.id === Number(f?.movieId))
  if (!f?.startsAt || !m) return ''
  const d = new Date(f.startsAt)
  d.setMinutes(d.getMinutes() + m.durationMinutes + 15)
  return fmtTime(`${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`)
})

async function save() {
  const f = form.value
  const body = (startsAt) => ({ movieId: Number(f.movieId), hallId: Number(f.hallId), startsAt, priceFactor: Number(f.priceFactor) })
  saving.value = true
  try {
    if (f.id) {
      await api.put(`/api/admin/showtimes/${f.id}`, body(f.startsAt))
      toast('سانس ویرایش شد.')
    } else {
      // همان سانس برای چند روز پیاپی
      let ok = 0
      const problems = []
      for (let d = 0; d < Math.max(1, Number(f.repeatDays)); d++) {
        try { await api.post('/api/admin/showtimes', body(addDays(f.startsAt, d))); ok++ } catch (e) { problems.push(e.message) }
      }
      if (ok) toast(`${num(ok)} سانس ثبت شد.`)
      if (problems.length) toast(problems[0] + (problems.length > 1 ? ` (و ${num(problems.length - 1)} خطای دیگر)` : ''), 'error')
      if (problems.length && !ok) return
    }
    form.value = null
    await loadList()
  } catch (e) { toast(e.message, 'error') } finally { saving.value = false }
}
async function remove(s) {
  const ok = await confirmDialog({ title: 'حذف سانس', message: `سانس ${fmtTime(s.startsAt)} فیلم «${s.movie}» حذف شود؟`, confirmText: 'حذف', danger: true })
  if (!ok) return
  try { await api.del(`/api/admin/showtimes/${s.id}`); toast('سانس حذف شد.'); await loadList() } catch (e) { toast(e.message, 'error') }
}
const pct = (s) => (s.capacity ? Math.min(100, Math.round((s.bookedSeats / s.capacity) * 100)) : 0)
</script>

<template>
  <div class="page-head">
    <div><h1>سانس‌ها</h1><p class="muted">نمایش هر فیلم در یک سالن و زمان مشخص.</p></div>
    <button class="btn" :disabled="!canCreate" :title="canCreate ? '' : 'ابتدا یک فیلم و یک سالن دارای چینش لازم است'" @click="startNew">
      <Icon name="plus" :size="17" /> سانس جدید
    </button>
  </div>
  <div v-if="movies && cinemas && !canCreate && showtimes" class="alert info" style="margin-bottom: 14px">
    برای ثبت سانس، ابتدا یک فیلم و یک سالن دارای چینش صندلی لازم است.
  </div>

  <div class="filters">
    <select v-model="filter.cinemaId" aria-label="سینما"><option value="">همهٔ سینماها</option><option v-for="c in cinemas" :key="c.id" :value="c.id">{{ c.name }}</option></select>
    <select v-model="filter.movieId" aria-label="فیلم"><option value="">همهٔ فیلم‌ها</option><option v-for="m in movies" :key="m.id" :value="m.id">{{ m.title }}</option></select>
    <label class="check"><input v-model="filter.past" type="checkbox" /> سانس‌های گذشته هم نمایش داده شود</label>
  </div>

  <Skeleton v-if="!showtimes" variant="panels" />
  <div v-else-if="!days.length" class="panel empty">سانسی با این فیلتر پیدا نشد.</div>

  <section v-for="d in days" :key="d.key" class="day">
    <h3>{{ d.label }} <span class="muted small">({{ num(d.items.length) }} سانس)</span></h3>
    <div class="panel">
      <div v-for="s in d.items" :key="s.id" class="list-row">
        <b class="time">{{ fmtTime(s.startsAt) }}</b>
        <div class="grow">
          <button class="movie-link" title="فقط سانس‌های این فیلم" @click="filter.movieId = s.movieId">{{ s.movie }}</button>
          <div class="muted small">{{ s.cinema }} · {{ s.hall }}</div>
        </div>
        <span class="badge" title="ضریب قیمت">{{ num(s.priceFactor) }}×</span>
        <RouterLink class="occ" :to="{ name: 'admin-sales', query: { showtimeId: s.id } }" title="بلیت‌های فروخته‌شدهٔ این سانس">
          <span class="small muted">{{ num(s.bookedSeats) }} / {{ num(s.capacity) }} بلیت</span>
          <div class="meter"><i :style="{ width: pct(s) + '%' }" /></div>
        </RouterLink>
        <button class="iconbtn" title="ویرایش" aria-label="ویرایش سانس" @click="startEdit(s)"><Icon name="edit" :size="17" /></button>
        <button class="iconbtn danger" title="حذف" aria-label="حذف سانس" @click="remove(s)"><Icon name="trash" :size="17" /></button>
      </div>
    </div>
  </section>

  <Modal v-if="form" :title="form.id ? 'ویرایش سانس' : 'سانس جدید'" @close="form = null">
    <form id="st-form" class="stack" @submit.prevent="save">
      <label class="field">فیلم
        <select v-model="form.movieId" required><option v-for="m in movies" :key="m.id" :value="m.id">{{ m.title }}</option></select>
      </label>
      <label class="field">سینما و سالن
        <select v-model="form.hallId" required>
          <optgroup v-for="c in cinemas" :key="c.id" :label="c.name">
            <option v-for="h in c.halls" :key="h.id" :value="h.id" :disabled="!h.rowCount">{{ h.name }}{{ h.rowCount ? '' : ' (چینش ندارد)' }}</option>
          </optgroup>
        </select>
      </label>
      <label class="field">زمان شروع
        <JalaliDateTime v-model="form.startsAt" required />
        <span v-if="endsAt" class="field-hint">پایان تقریبی: {{ endsAt }} (با ۱۵ دقیقه پاک‌سازی سالن)</span>
      </label>

      <div class="field">
        <span>ضریب قیمت</span>
        <div class="presets">
          <button v-for="p in PRESETS" :key="p.f" type="button" :class="{ on: Number(form.priceFactor) === p.f }" @click="form.priceFactor = p.f">{{ p.label }} <small>{{ p.hint }}</small></button>
          <input v-model="form.priceFactor" type="number" step="0.05" min="0.1" max="10" required aria-label="ضریب قیمت" />
        </div>
        <span class="field-hint">قیمت هر صندلی = قیمت ردیف در چینش سالن × این ضریب.</span>
      </div>

      <label v-if="!form.id" class="field">تکرار برای چند روز پیاپی
        <input v-model="form.repeatDays" type="number" min="1" max="60" />
        <span class="field-hint">۱ یعنی فقط همین روز.</span>
      </label>
    </form>
    <template #footer>
      <button class="btn" form="st-form" :disabled="saving">ذخیره</button>
      <button class="btn secondary" type="button" @click="form = null">انصراف</button>
    </template>
  </Modal>
</template>

<style scoped>
.filters { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; margin-bottom: 18px; }
.filters select { width: auto; min-width: 180px; }
.check { display: inline-flex; align-items: center; gap: 8px; color: var(--muted); font-size: .9rem; cursor: pointer; }
.check input { width: auto; }
.day { margin-bottom: 18px; }
.day h3 { margin-bottom: 8px; }
.time { min-width: 52px; font-size: 1.1rem; color: var(--sky); }
.occ { display: grid; gap: 4px; min-width: 110px; color: inherit; }
.occ:hover { text-decoration: none; }
.occ:hover span { color: var(--sky); }
.movie-link { background: none; border: 0; padding: 0; font: inherit; font-weight: 700; color: var(--text); cursor: pointer; text-align: start; }
.movie-link:hover { color: var(--sky); }
.presets { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
.presets button { font: inherit; display: inline-flex; gap: 6px; align-items: center; padding: 4px 12px; border-radius: 8px; border: 1px solid var(--border); background: transparent; color: var(--muted); cursor: pointer; }
.presets button small { opacity: .7; }
.presets button:hover { color: var(--text); border-color: #34405a; }
.presets button.on { background: var(--primary-soft); border-color: #1f3a68; color: #9cc2ff; }
.presets input { width: 90px; }
.empty { padding: 30px; }
@media (max-width: 640px) {
  .filters select { flex: 1 1 100%; min-width: 0; }
  .list-row { display: grid; grid-template-columns: auto minmax(0, 1fr) auto auto; gap: 8px 10px; align-items: center; }
  .time { grid-column: 1; grid-row: 1; min-width: 0; }
  .list-row > .grow { grid-column: 2 / -1; grid-row: 1; min-width: 0; }
  .list-row > .badge { grid-column: 1; grid-row: 2; justify-self: start; }
  .occ { grid-column: 2; grid-row: 2; min-width: 0; }
  .list-row > .iconbtn { grid-row: 2; }
}
</style>
