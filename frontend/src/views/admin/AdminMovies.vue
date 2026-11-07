<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import Modal from '../../components/Modal.vue'
import Icon from '../../components/Icon.vue'
import PosterArt from '../../components/PosterArt.vue'
import MovieImageField from '../../components/MovieImageField.vue'
import AgeBadge from '../../components/AgeBadge.vue'
import NoteChip from '../../components/NoteChip.vue'
import { genreList, AGE_OPTIONS } from '../../lib/genre'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { num, fmtDate, fmtTime } from '../../lib/format'

const router = useRouter()
const GENRES = ['اکشن', 'انیمیشن', 'درام', 'کمدی', 'ترسناک', 'علمی‌تخیلی', 'ماجراجویی', 'عاشقانه', 'جنایی', 'فانتزی', 'هیجان‌انگیز', 'خانوادگی', 'معمایی', 'تاریخی', 'جنگی', 'مستند']
const MAX_GENRES = 3
const NOTE_EXAMPLES = ['با دوبله محبوب سورن', 'اکران ویژهٔ این هفته', 'پرطرفدارترین فیلم هفته', 'با زیرنویس فارسی']
const genreInput = ref('')

// انتخاب ژانر: حداکثر ۳ تا؛ از پیشنهادها یا تایپ دستی
function addGenre(g) {
  const name = (g ?? genreInput.value).replace(/[,،]/g, '').trim()
  genreInput.value = ''
  if (!name || form.value.genres.includes(name) || form.value.genres.length >= MAX_GENRES) return
  form.value.genres.push(name)
}
const removeGenre = (g) => (form.value.genres = form.value.genres.filter((x) => x !== g))

const movies = ref(null)
const form = ref(null)
const q = ref('')
const saving = ref(false)

async function load() {
  try { movies.value = await api.get('/api/admin/movies') } catch (e) { toast(e.message, 'error') }
}

// ---------- TMDB (اختیاری) ----------
const tmdb = ref({ configured: false, imageBase: '' })
const tq = ref('')
const tResults = ref(null)
const tBusy = ref(false)
const tError = ref('')
const tNote = ref('')

onMounted(async () => {
  load()
  try { tmdb.value = await api.get('/api/admin/tmdb/status') } catch { /* بدون TMDB ادامه می‌دهیم */ }
})

async function tSearch() {
  if (tq.value.trim().length < 2) return
  tBusy.value = true; tError.value = ''; tNote.value = ''; tResults.value = null
  try { tResults.value = await api.get(`/api/admin/tmdb/search?q=${encodeURIComponent(tq.value.trim())}`) }
  catch (e) { tError.value = e.message } finally { tBusy.value = false }
}
async function tPick(r) {
  tBusy.value = true; tError.value = ''; tNote.value = ''
  try {
    const d = await api.get(`/api/admin/tmdb/movie/${r.tmdbId}`)
    const f = form.value
    f.title = d.title || r.title
    f.originalTitle = d.originalTitle
    if (d.genres?.length) f.genres = d.genres.slice(0, MAX_GENRES)
    if (d.ageRating) f.ageRating = d.ageRating
    if (d.durationMinutes > 0) f.durationMinutes = d.durationMinutes
    f.description = d.overview
    f.releaseYear = d.year
    f.tmdbId = d.tmdbId
    f.poster = { ...f.poster, tmdbPath: d.posterPath ?? '', file: null }
    f.backdrop = { ...f.backdrop, tmdbPath: d.backdropPath ?? '', file: null }
    const notes = []
    if (!/[؀-ۿ]/.test(f.title)) notes.push('عنوان فارسی در TMDB ثبت نشده؛ در صورت نیاز آن را ویرایش کنید.')
    if (d.ageRating) notes.push('رده سنی از روی رتبه‌بندی آمریکا حدس زده شده؛ در صورت نیاز تغییر دهید.')
    if (d.overviewFallback) notes.push('خلاصهٔ فارسی در TMDB نبود؛ متن انگلیسی گذاشته شد.')
    if (!d.posterPath || !d.backdropPath) notes.push('برای بعضی تصاویر در TMDB چیزی ثبت نشده؛ می‌توانید دستی بگذارید.')
    tNote.value = notes.join(' ')
    tResults.value = null
  } catch (e) { tError.value = e.message } finally { tBusy.value = false }
}

// ---------- فرم ----------
const slot = (url = '') => ({ url, tmdbPath: '', file: null })
const blank = () => ({ id: null, title: '', originalTitle: '', genres: [], ageRating: '', specialNote: '', durationMinutes: 100, description: '', releaseYear: null, tmdbId: null, poster: slot(), backdrop: slot() })
const openForm = (f) => { genreInput.value = ''; tq.value = ''; tResults.value = null; tError.value = ''; tNote.value = ''; form.value = f }
const edit = (m) => openForm({ ...m, genres: genreList(m.genre), ageRating: m.ageRating ?? '', specialNote: m.specialNote ?? '', poster: slot(m.posterUrl), backdrop: slot(m.backdropUrl) })

const shown = computed(() => (movies.value ?? []).filter((m) => !q.value || m.title.includes(q.value.trim()) || m.originalTitle?.toLowerCase().includes(q.value.trim().toLowerCase())))
const addShowtime = (m) => router.push({ name: 'admin-showtimes', query: { movieId: m.id, new: 1 } })

async function save() {
  const f = form.value
  saving.value = true
  try {
    const dto = {
      title: f.title, originalTitle: f.originalTitle, genre: f.genres.join(','), ageRating: f.ageRating, specialNote: f.specialNote, description: f.description,
      durationMinutes: Number(f.durationMinutes), releaseYear: f.releaseYear ? Number(f.releaseYear) : null, tmdbId: f.tmdbId,
      posterUrl: f.poster.url, backdropUrl: f.backdrop.url,
      tmdbPosterPath: f.poster.file ? null : f.poster.tmdbPath || null,
      tmdbBackdropPath: f.backdrop.file ? null : f.backdrop.tmdbPath || null,
    }
    const res = await (f.id ? api.put(`/api/admin/movies/${f.id}`, dto) : api.post('/api/admin/movies', dto))
    const id = f.id ?? res.id
    const problems = [...(res.warnings ?? [])]

    // فایل‌های آپلودی بعد از ساخت فیلم ارسال می‌شوند
    for (const [kind, label] of [['poster', 'کاور'], ['backdrop', 'بک‌دراپ']]) {
      const file = f[kind].file
      if (!file) continue
      try {
        const fd = new FormData()
        fd.append('file', file)
        await api.upload(`/api/admin/movies/${id}/images/${kind}`, fd)
      } catch (e) { problems.push(`${label}: ${e.message}`) }
    }

    toast(f.id ? 'فیلم ویرایش شد.' : 'فیلم اضافه شد.')
    if (problems.length) toast(`فیلم ذخیره شد، اما: ${problems.join(' | ')} — می‌توانید تصویر را دستی بگذارید.`, 'error')
    form.value = null
    await load()
  } catch (e) { toast(e.message, 'error') } finally { saving.value = false }
}

async function remove(m) {
  const ok = await confirmDialog({ title: 'حذف فیلم', message: `فیلم «${m.title}» حذف شود؟ کاور و بک‌دراپ ذخیره‌شدهٔ آن هم پاک می‌شود.`, confirmText: 'حذف', danger: true })
  if (!ok) return
  try { await api.del(`/api/admin/movies/${m.id}`); toast('فیلم حذف شد.'); await load() } catch (e) { toast(e.message, 'error') }
}
</script>

<template>
  <div class="page-head">
    <div><h1>فیلم‌ها</h1><p class="muted">فهرست فیلم‌هایی که می‌توان برایشان سانس ثبت کرد.</p></div>
    <button class="btn" @click="openForm(blank())"><Icon name="plus" :size="17" /> فیلم جدید</button>
  </div>

  <Skeleton v-if="!movies" variant="panels" />
  <div v-else-if="!movies.length" class="panel empty">
    <p>هنوز فیلمی ثبت نشده است.</p>
    <button class="btn" @click="openForm(blank())">اولین فیلم را اضافه کنید</button>
  </div>

  <template v-else>
    <div class="search" style="margin-bottom: 12px">
      <Icon name="search" :size="17" />
      <input v-model="q" placeholder="جست‌وجوی فیلم…" aria-label="جست‌وجوی فیلم" />
    </div>

    <section class="panel">
      <div v-for="m in shown" :key="m.id" class="list-row">
        <div class="thumb"><PosterArt :movie="m" :show-title="false" /></div>

        <div class="grow">
          <div class="title">
            <b>{{ m.title }}</b>
            <RouterLink class="site" :to="{ name: 'movie', params: { id: m.id } }" title="مشاهده در سایت" aria-label="مشاهده در سایت"><Icon name="external" :size="14" /></RouterLink>
          </div>
          <div v-if="m.specialNote" class="notewrap"><NoteChip :note="m.specialNote" size="sm" /></div>
          <div class="muted small genre"><Icon :genre="m.genre" :size="14" /> {{ genreList(m.genre).join(' · ') || 'بدون ژانر' }} · {{ num(m.durationMinutes) }} دقیقه<template v-if="m.releaseYear"> · {{ m.releaseYear }}</template> <AgeBadge :code="m.ageRating" /></div>
        </div>

        <div class="counts">
          <RouterLink class="count" :class="{ zero: !m.upcomingShowtimes }" :to="{ name: 'admin-showtimes', query: { movieId: m.id } }"
            :title="m.nextStartsAt ? `سانس بعدی: ${fmtDate(m.nextStartsAt)} ${fmtTime(m.nextStartsAt)}` : 'سانس پیش‌رویی ندارد'">
            <b>{{ num(m.upcomingShowtimes) }}</b><span>سانس پیش رو</span>
          </RouterLink>
          <RouterLink class="count" :class="{ zero: !m.bookings }" :to="{ name: 'admin-sales', query: { movieId: m.id } }">
            <b>{{ num(m.bookings) }}</b><span>رزرو</span>
          </RouterLink>
        </div>

        <div class="acts">
          <button class="btn secondary sm" @click="addShowtime(m)"><Icon name="plus" :size="15" /> سانس</button>
          <button class="iconbtn" title="ویرایش" aria-label="ویرایش فیلم" @click="edit(m)"><Icon name="edit" :size="17" /></button>
          <button class="iconbtn danger" title="حذف" aria-label="حذف فیلم" @click="remove(m)"><Icon name="trash" :size="17" /></button>
        </div>
      </div>
      <div v-if="!shown.length" class="empty">فیلمی با این نام پیدا نشد.</div>
    </section>
  </template>

  <Modal v-if="form" :title="form.id ? 'ویرایش فیلم' : 'فیلم جدید'" width="820px" @close="form = null">
    <!-- ۱) جست‌وجو در TMDB -->
    <section class="tmdb">
      <div class="thead"><b>پر کردن خودکار از TMDB</b><span class="badge">اختیاری</span></div>

      <template v-if="tmdb.configured">
        <form class="row" @submit.prevent="tSearch">
          <input v-model="tq" class="grow" placeholder="نام فیلم (فارسی یا انگلیسی)" aria-label="جست‌وجو در TMDB" />
          <button class="btn secondary" :disabled="tBusy || tq.trim().length < 2">{{ tBusy ? 'در حال جست‌وجو…' : 'جست‌وجو' }}</button>
        </form>

        <div v-if="tError" class="alert error small">{{ tError }} <b>اطلاعات و تصاویر را می‌توانید دستی وارد کنید.</b></div>
        <div v-if="tNote" class="alert info small">{{ tNote }}</div>

        <ul v-if="tResults" class="results">
          <li v-if="!tResults.length" class="muted small">نتیجه‌ای پیدا نشد؛ با نام انگلیسی یا دستی وارد کنید.</li>
          <li v-for="r in tResults" :key="r.tmdbId">
            <button type="button" @click="tPick(r)">
              <span class="rt"><img v-if="r.posterPath" :src="`${tmdb.imageBase}/w92${r.posterPath}`" alt="" loading="lazy" @error="$event.target.style.visibility = 'hidden'" /></span>
              <span class="rinfo"><b>{{ r.title }}</b><span class="muted small" dir="ltr">{{ r.originalTitle }}<template v-if="r.year"> · {{ r.year }}</template></span></span>
            </button>
          </li>
        </ul>
      </template>
      <p v-else class="muted small" style="margin: 0">
        جست‌وجوی TMDB فعال نیست (کلید API تنظیم نشده). مشکلی نیست؛ اطلاعات و تصاویر را در پایین دستی وارد کنید.
        برای فعال‌سازی، کلید را در <code dir="ltr">Tmdb:ApiKey</code> بگذارید.
      </p>
      <p class="muted small attr">اطلاعات و تصاویر از TMDB گرفته می‌شود؛ این سرویس از API سایت TMDB استفاده می‌کند ولی توسط TMDB تأیید یا پشتیبانی نشده است.</p>
    </section>

    <!-- ۲) فرم -->
    <form id="movie-form" class="mform" @submit.prevent="save">
      <div class="fields">
        <label class="field">عنوان (فارسی) <input v-model="form.title" required /></label>
        <label class="field">عنوان اصلی <input v-model="form.originalTitle" dir="ltr" placeholder="Original title" /></label>
        <div class="three">
          <label class="field">مدت (دقیقه) <input v-model="form.durationMinutes" type="number" min="1" max="600" required /></label>
          <label class="field">سال <input v-model="form.releaseYear" type="number" min="1888" max="2100" dir="ltr" /></label>
          <label class="field">رده سنی
            <select v-model="form.ageRating"><option v-for="o in AGE_OPTIONS" :key="o.v" :value="o.v">{{ o.label }}</option></select>
          </label>
        </div>

        <div class="field genres">
          <span>ژانرها <small>(حداکثر {{ MAX_GENRES }}؛ اولی ژانر اصلی است)</small></span>
          <div class="gchips">
            <span v-for="g in form.genres" :key="g" class="gchip on">
              <Icon :genre="g" :size="14" /> {{ g }}
              <button type="button" :aria-label="`حذف ${g}`" @click="removeGenre(g)"><Icon name="x" :size="12" /></button>
            </span>
            <input v-if="form.genres.length < MAX_GENRES" v-model="genreInput" class="ginput" placeholder="ژانر دیگر… (Enter)" aria-label="افزودن ژانر" @keydown.enter.prevent="addGenre()" />
          </div>
          <div v-if="form.genres.length < MAX_GENRES" class="suggest">
            <button v-for="g in GENRES.filter((x) => !form.genres.includes(x))" :key="g" type="button" @click="addGenre(g)">+ {{ g }}</button>
          </div>
        </div>
        <div class="field">
          <span>توضیح ویژه <small>(اختیاری)</small></span>
          <input v-model="form.specialNote" maxlength="120" placeholder="مثلاً: با دوبله محبوب سورن" aria-label="توضیح ویژه" />
          <span class="field-hint">با رنگ طلایی در اسلایدر صفحهٔ اول، صفحهٔ فیلم و صفحهٔ رزرو نشان داده می‌شود. {{ form.specialNote.length }}/120</span>
          <div class="suggest">
            <button v-for="e in NOTE_EXAMPLES" :key="e" type="button" @click="form.specialNote = e">{{ e }}</button>
          </div>
        </div>

        <label class="field">توضیحات <textarea v-model="form.description" rows="5" /></label>
      </div>

      <div class="images">
        <MovieImageField v-model="form.poster" label="کاور (پوستر)" kind="poster" :image-base="tmdb.imageBase" />
        <MovieImageField v-model="form.backdrop" label="تصویر پس‌زمینه (بک‌دراپ)" kind="backdrop" :image-base="tmdb.imageBase" />
      </div>
    </form>

    <template #footer>
      <button class="btn" form="movie-form" :disabled="saving">{{ saving ? 'در حال ذخیره…' : 'ذخیره' }}</button>
      <button class="btn secondary" type="button" @click="form = null">انصراف</button>
    </template>
  </Modal>
</template>

<style scoped>
.search { display: flex; align-items: center; gap: 8px; color: var(--muted); background: var(--surface-2); border: 1px solid var(--border); border-radius: 10px; padding: 0 12px; max-width: 340px; }
.search input { border: 0; padding: 8px 0; background: none; box-shadow: none !important; }
.search:focus-within { border-color: var(--primary); }
.thumb { width: 40px; flex: none; border-radius: 6px; overflow: hidden; border: 1px solid var(--border); }
.title { display: flex; align-items: center; gap: 8px; }
.site { display: inline-grid; color: var(--muted); }
.site:hover { color: var(--sky); }
.counts { display: flex; gap: 6px; }
.count { display: grid; justify-items: center; min-width: 92px; padding: 4px 10px; border-radius: 10px; color: var(--text); line-height: 1.35; transition: background .15s; }
.count:hover { background: var(--surface-2); text-decoration: none; }
.count b { font-size: 1.15rem; }
.count span { font-size: .75rem; color: var(--muted); }
.count.zero b { color: var(--muted); font-weight: 400; }
.count:not(.zero):hover b { color: var(--sky); }
.acts { display: flex; align-items: center; gap: 2px; }
.acts .btn { margin-inline-end: 6px; }
.genre { display: flex; align-items: center; gap: 5px; }
.empty p { margin-bottom: 12px; }

.tmdb { display: grid; gap: 10px; padding: 14px; border-radius: 12px; background: var(--surface-2); border: 1px solid var(--border); }
.thead { display: flex; align-items: center; gap: 8px; }
.attr { margin: 0; font-size: .72rem; opacity: .8; }
code { background: var(--surface); padding: 0 6px; border-radius: 4px; }
.results { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; max-height: 260px; overflow-y: auto; }
.results button { width: 100%; display: flex; align-items: center; gap: 12px; text-align: start; padding: 6px 8px; border-radius: 10px; border: 1px solid transparent; background: transparent; color: var(--text); font: inherit; cursor: pointer; }
.results button:hover { background: var(--surface); border-color: var(--border); }
.rt { width: 38px; aspect-ratio: 2 / 3; border-radius: 4px; background: var(--surface); flex: none; overflow: hidden; }
.rt img { width: 100%; height: 100%; object-fit: cover; display: block; }
.rinfo { display: grid; line-height: 1.5; }

.mform { display: grid; grid-template-columns: minmax(0, 1fr) 260px; gap: 20px; }
.fields { display: grid; gap: 14px; align-content: start; }
.three { display: grid; grid-template-columns: 1fr 1fr 1.2fr; gap: 12px; }
.genres small { opacity: .7; }
.notewrap { margin: 2px 0 3px; }
.gchips { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.gchip { display: inline-flex; align-items: center; gap: 6px; padding: 3px 6px 3px 10px; border-radius: 999px; font-size: .85rem; background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; }
.gchip button { display: inline-grid; place-items: center; width: 18px; height: 18px; padding: 0; border: 0; border-radius: 50%; background: transparent; color: inherit; cursor: pointer; }
.gchip button:hover { background: rgb(255 255 255 / 12%); }
.ginput { width: 170px; padding: 4px 10px; border-radius: 999px; font-size: .85rem; }
.suggest { display: flex; flex-wrap: wrap; gap: 6px; }
.suggest button { font: inherit; font-size: .8rem; padding: 2px 10px; border-radius: 999px; border: 1px dashed var(--border); background: transparent; color: var(--muted); cursor: pointer; }
.suggest button:hover { color: var(--text); border-color: #34405a; }
.images { display: grid; gap: 16px; align-content: start; }
@media (max-width: 720px) {
  .search { max-width: none; width: 100%; box-sizing: border-box; }
  .mform { grid-template-columns: 1fr; }
  .three { grid-template-columns: 1fr 1fr; }
  .list-row { display: grid; grid-template-columns: 64px minmax(0, 1fr); gap: 8px 12px; align-items: start; }
  .list-row > .grow { min-width: 0; }
  .thumb { width: 64px; grid-row: 1 / 3; }
  .genre { display: block; line-height: 1.8; font-size: .78rem; }
  .counts { grid-column: 2; gap: 6px; align-self: end; }
  .count { flex: 1; flex-direction: row; display: flex; align-items: baseline; justify-content: center; gap: 6px; min-width: 0; padding: 5px 8px; background: var(--surface-2); border-radius: 8px; }
  .count b { font-size: 1rem; }
  .acts { grid-column: 1 / -1; display: flex; gap: 6px; padding-top: 8px; border-top: 1px solid var(--border); }
  .acts .btn { flex: 1; justify-content: center; margin: 0; }
}
</style>
