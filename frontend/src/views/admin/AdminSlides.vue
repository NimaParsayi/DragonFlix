<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, computed, watch, onMounted } from 'vue'
import Modal from '../../components/Modal.vue'
import Icon from '../../components/Icon.vue'
import PosterArt from '../../components/PosterArt.vue'
import NoteChip from '../../components/NoteChip.vue'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { genreList } from '../../lib/genre'

const MAX_SLIDES = 8
const NOTE_MAX = 120
const EXAMPLES = ['با دوبله محبوب سورن', 'اکران ویژهٔ این هفته', 'پرطرفدارترین فیلم هفته', 'با زیرنویس فارسی']

const slides = ref(null)
const movies = ref([])
const form = ref(null) // { id|null, movieId, note, active }
const saving = ref(false)

async function load() {
  try { slides.value = await api.get('/api/admin/slides') } catch (e) { toast(e.message, 'error') }
}
onMounted(async () => {
  load()
  try { movies.value = await api.get('/api/admin/movies') } catch (e) { toast(e.message, 'error') }
})

const available = computed(() => movies.value.filter((m) => !(slides.value ?? []).some((s) => s.movieId === m.id)))
const full = computed(() => (slides.value?.length ?? 0) >= MAX_SLIDES)

// در افزودن، توضیحی که فیلم از قبل دارد جلوی چشم می‌آید (مگر کاربر خودش چیز دیگری نوشته باشد)
const noteOf = (id) => movies.value.find((m) => m.id === Number(id))?.specialNote ?? ''
let prevMovie = null
watch(() => form.value?.movieId, (id) => {
  if (!form.value || form.value.id) return
  if (!form.value.note || form.value.note === noteOf(prevMovie)) form.value.note = noteOf(id)
  prevMovie = id
})

function openAdd() {
  const first = available.value[0]
  prevMovie = first?.id ?? null
  form.value = { id: null, movieId: first?.id ?? '', note: first?.specialNote ?? '', active: true }
}
const openEdit = (s) => (form.value = { id: s.id, movieId: s.movieId, note: s.note, active: s.active })

async function save() {
  const f = form.value
  saving.value = true
  try {
    if (f.id) await api.put(`/api/admin/slides/${f.id}`, { note: f.note, active: f.active })
    else await api.post('/api/admin/slides', { movieId: Number(f.movieId), note: f.note })
    toast(f.id ? 'اسلاید ویرایش شد.' : 'فیلم به اسلایدر اضافه شد.')
    form.value = null
    await load()
  } catch (e) { toast(e.message, 'error') } finally { saving.value = false }
}

async function toggle(s) {
  try { await api.put(`/api/admin/slides/${s.id}`, { note: s.note, active: !s.active }); await load() } catch (e) { toast(e.message, 'error') }
}

async function move(i, d) {
  const j = i + d
  const list = [...slides.value]
  if (j < 0 || j >= list.length) return
  ;[list[i], list[j]] = [list[j], list[i]]
  slides.value = list // فوری نشان بده، بعد ذخیره کن
  try { await api.put('/api/admin/slides/order', { ids: list.map((s) => s.id) }) } catch (e) { toast(e.message, 'error'); await load() }
}

async function remove(s) {
  const ok = await confirmDialog({ title: 'حذف از اسلایدر', message: `«${s.movie.title}» از اسلایدر برداشته شود؟ خود فیلم حذف نمی‌شود.`, confirmText: 'حذف', danger: true })
  if (!ok) return
  try { await api.del(`/api/admin/slides/${s.id}`); toast('از اسلایدر برداشته شد.'); await load() } catch (e) { toast(e.message, 'error') }
}
</script>

<template>
  <div class="page-head">
    <div>
      <h1>اسلایدر صفحهٔ اول</h1>
      <p class="muted">فیلم‌هایی که بالای صفحهٔ اول می‌چرخند، به‌همراه یک توضیح ویژه برای هرکدام.</p>
    </div>
    <button class="btn" :disabled="full || !available.length" :title="full ? `حداکثر ${MAX_SLIDES} اسلاید` : !available.length ? 'همهٔ فیلم‌ها در اسلایدرند' : ''" @click="openAdd">
      <Icon name="plus" :size="17" /> افزودن فیلم
    </button>
  </div>

  <Skeleton v-if="!slides" variant="panels" />

  <div v-else-if="!slides.length" class="panel empty">
    <Icon name="sparkles" :size="30" />
    <p>هنوز اسلایدی نچیده‌اید.</p>
    <p class="muted small">تا آن موقع، سایت خودکار ۴ فیلم دارای بیشترین سانس را در اسلایدر نشان می‌دهد.</p>
    <button class="btn" :disabled="!movies.length" @click="openAdd">اولین فیلم را اضافه کنید</button>
  </div>

  <section v-else class="panel">
    <div v-for="(s, i) in slides" :key="s.id" class="list-row" :class="{ off: !s.active }">
      <div class="order">
        <button class="iconbtn" :disabled="i === 0" aria-label="بالاتر" @click="move(i, -1)"><Icon name="up" :size="16" /></button>
        <span>{{ i + 1 }}</span>
        <button class="iconbtn" :disabled="i === slides.length - 1" aria-label="پایین‌تر" @click="move(i, 1)"><Icon name="down" :size="16" /></button>
      </div>

      <div class="thumb">
        <img v-if="s.movie.backdropUrl" :src="s.movie.backdropUrl" alt="" />
        <PosterArt v-else :movie="s.movie" :show-title="false" />
      </div>

      <div class="grow">
        <div class="ttl">
          <b>{{ s.movie.title }}</b>
          <span class="muted small">{{ genreList(s.movie.genre).join(' · ') }}</span>
        </div>
        <NoteChip v-if="s.note" :note="s.note" size="sm" />
        <div v-else class="muted small">بدون توضیح ویژه</div>
        <div v-if="!s.upcomingShowtimes" class="warn small"><Icon name="alert" :size="13" /> سانس پیش‌رو ندارد؛ تا سانس ثبت نشود در سایت دیده نمی‌شود.</div>
      </div>

      <label class="switch" :title="s.active ? 'فعال؛ برای پنهان‌کردن بزنید' : 'غیرفعال؛ برای نمایش بزنید'">
        <input type="checkbox" :checked="s.active" @change="toggle(s)" />
        <span class="knob" /><span class="lbl">{{ s.active ? 'فعال' : 'پنهان' }}</span>
      </label>
      <button class="iconbtn" title="ویرایش توضیح" aria-label="ویرایش اسلاید" @click="openEdit(s)"><Icon name="edit" :size="17" /></button>
      <button class="iconbtn danger" title="برداشتن از اسلایدر" aria-label="حذف از اسلایدر" @click="remove(s)"><Icon name="trash" :size="17" /></button>
    </div>
  </section>

  <p v-if="slides?.length" class="muted small" style="margin-top: 12px">
    {{ slides.length }} از {{ MAX_SLIDES }} اسلاید. ترتیب این لیست همان ترتیب چرخش در سایت است.
  </p>

  <Modal v-if="form" :title="form.id ? 'ویرایش اسلاید' : 'افزودن فیلم به اسلایدر'" width="520px" @close="form = null">
    <form id="slide-form" class="stack" @submit.prevent="save">
      <label v-if="!form.id" class="field">فیلم
        <select v-model="form.movieId" required>
          <option v-for="m in available" :key="m.id" :value="m.id">{{ m.title }}</option>
        </select>
      </label>
      <label class="field">
        <span>توضیح ویژه <small>(اختیاری)</small></span>
        <input v-model="form.note" :maxlength="NOTE_MAX" placeholder="مثلاً: با دوبله محبوب سورن" autofocus />
        <span class="field-hint">این توضیح روی خود فیلم ذخیره می‌شود؛ پس علاوه بر اسلایدر، در صفحهٔ فیلم و صفحهٔ رزرو هم دیده می‌شود. {{ form.note.length }}/{{ NOTE_MAX }}</span>
      </label>
      <div class="ex">
        <span class="muted small">نمونه:</span>
        <button v-for="e in EXAMPLES" :key="e" type="button" @click="form.note = e">{{ e }}</button>
      </div>
      <div v-if="form.note" class="preview"><NoteChip :note="form.note" /></div>
    </form>
    <template #footer>
      <button class="btn" form="slide-form" :disabled="saving || (!form.id && !form.movieId)">ذخیره</button>
      <button class="btn secondary" type="button" @click="form = null">انصراف</button>
    </template>
  </Modal>
</template>

<style scoped>
.empty { display: grid; justify-items: center; gap: 6px; padding: 36px 16px; color: var(--muted); text-align: center; }
.empty p { margin: 0; }
.empty .btn { margin-top: 10px; }
.list-row.off { opacity: .55; }
.order { display: grid; justify-items: center; gap: 0; color: var(--muted); font-size: .85rem; }
.thumb { width: 96px; aspect-ratio: 16 / 9; flex: none; border-radius: 8px; overflow: hidden; border: 1px solid var(--border); background: var(--surface-2); display: grid; place-items: center; }
.thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
.thumb :deep(.poster) { aspect-ratio: 16 / 9; }
.ttl { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.warn { display: flex; align-items: center; gap: 5px; margin-top: 4px; color: var(--warn); }

.switch { display: inline-flex; align-items: center; gap: 8px; cursor: pointer; font-size: .8rem; color: var(--muted); }
.switch { position: relative; }
.switch input { position: absolute; width: 1px; height: 1px; margin: 0; padding: 0; opacity: 0; pointer-events: none; }
.knob { position: relative; width: 34px; height: 20px; border-radius: 999px; background: var(--surface-2); border: 1px solid var(--border); transition: background .15s; flex: none; }
.knob::after { content: ''; position: absolute; top: 2px; inset-inline-start: 2px; width: 14px; height: 14px; border-radius: 50%; background: var(--muted); transition: all .15s; }
.switch input:checked + .knob { background: var(--primary); border-color: var(--primary); }
.switch input:checked + .knob::after { inset-inline-start: 16px; background: #fff; }
.switch input:focus-visible + .knob { outline: 2px solid var(--sky); outline-offset: 2px; }
.lbl { min-width: 32px; }

.ex { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.ex button { font: inherit; font-size: .8rem; padding: 2px 10px; border-radius: 999px; border: 1px dashed var(--border); background: transparent; color: var(--muted); cursor: pointer; }
.ex button:hover { color: var(--text); border-color: #34405a; }
.preview { padding: 10px; border-radius: 10px; background: var(--surface-2); border: 1px dashed var(--border); }
@media (max-width: 720px) {
  .list-row { display: grid; grid-template-columns: auto 84px minmax(0, 1fr) auto auto; gap: 8px 12px; align-items: center; }
  .thumb { width: 84px; }
  .list-row > .grow { grid-column: 3 / -1; min-width: 0; }
  .list-row > .switch { grid-column: 1 / 4; grid-row: 2; justify-self: start; }
  .list-row > .iconbtn { grid-row: 2; }
}
</style>
