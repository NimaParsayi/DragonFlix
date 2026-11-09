<script setup>
import Skeleton from '../components/Skeleton.vue'
import Icon from '../components/Icon.vue'
import HeroSlider from '../components/HeroSlider.vue'
import { ref, computed, onMounted } from 'vue'
import PosterArt from '../components/PosterArt.vue'
import { api } from '../lib/api'
import AgeBadge from '../components/AgeBadge.vue'
import { genreList } from '../lib/genre'
import { num, fmtDate, fmtTime } from '../lib/format'

const movies = ref([])
const slidesRaw = ref([])
const cinemas = ref([])
const loading = ref(true)
const error = ref('')
const q = ref('')
const genre = ref('')

onMounted(async () => {
  try {
    ;[movies.value, cinemas.value, slidesRaw.value] = await Promise.all([api.get('/api/movies'), api.get('/api/cinemas'), api.get('/api/slides').catch(() => [])])
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
})

// اسلایدهایی که ادمین چیده؛ اگر چیزی نچیده باشد، فیلم‌های دارای بیشترین سانس به‌عنوان پیش‌فرض نشان داده می‌شود.
const slides = computed(() => {
  if (slidesRaw.value.length) return slidesRaw.value
  return [...movies.value].filter((m) => m.upcomingShowtimes > 0).sort((a, b) => b.upcomingShowtimes - a.upcomingShowtimes).slice(0, 4)
    .map((m) => ({ id: `auto-${m.id}`, note: '', movie: m, nextStartsAt: m.nextStartsAt }))
})
const genres = computed(() => [...new Set(movies.value.flatMap((m) => genreList(m.genre)))])
const filtered = computed(() =>
  movies.value.filter((m) => (!genre.value || genreList(m.genre).includes(genre.value)) && (!q.value || m.title.includes(q.value.trim()))),
)
</script>

<template>
  <div v-if="error" class="alert error">{{ error }}</div>
  <Skeleton v-else-if="loading" variant="home" />

  <template v-else>
    <HeroSlider v-if="slides.length" :slides="slides" />
    <div v-else class="hspace" aria-hidden="true" />

    <!-- فیلم‌ها -->
    <div class="section-title"><h2>فیلم‌های در حال اکران</h2></div>

    <div v-if="movies.length" class="toolbar">
      <div class="search">
        <Icon name="search" :size="18" />
        <input v-model="q" placeholder="جست‌وجوی فیلم…" aria-label="جست‌وجوی فیلم" />
      </div>
      <div class="chips" role="group" aria-label="ژانر">
        <button :class="{ on: !genre }" @click="genre = ''"><Icon name="film" :size="16" /> همه</button>
        <button v-for="g in genres" :key="g" :class="{ on: genre === g }" @click="genre = genre === g ? '' : g"><Icon :genre="g" :size="16" /> {{ g }}</button>
      </div>
    </div>

    <div v-if="!movies.length" class="empty card">هنوز فیلمی برای نمایش ثبت نشده است؛ به‌زودی برمی‌گردیم.</div>
    <div v-else-if="!filtered.length" class="empty card">فیلمی با این مشخصات پیدا نشد.</div>
    <div v-else class="grid">
      <RouterLink v-for="m in filtered" :key="m.id" :to="{ name: 'movie', params: { id: m.id } }" class="movie">
        <div class="pwrap">
          <PosterArt :movie="m" :show-title="false" />
          <span class="dur">{{ num(m.durationMinutes) }}′</span>
          <AgeBadge class="agepos" :code="m.ageRating" />
        </div>
        <h3>{{ m.title }}</h3>
        <div class="muted small genre" :title="genreList(m.genre).join(' · ')"><Icon :genre="m.genre" :size="14" /> <span class="gtxt">{{ genreList(m.genre).join(' · ') }}</span></div>
        <div class="small slots" :class="{ none: !m.upcomingShowtimes }">
          {{ m.upcomingShowtimes ? `${num(m.upcomingShowtimes)} سانس · از ${fmtTime(m.nextStartsAt)}` : 'سانسی ندارد' }}
        </div>
      </RouterLink>
    </div>

    <!-- سینماها -->
    <div v-if="cinemas.length" class="section-title"><h2>سینماها</h2></div>
    <div v-if="cinemas.length" class="cinemas">
      <article v-for="c in cinemas" :key="c.id" class="card cinema">
        <div class="ico"><Icon name="film" :size="24" /></div>
        <div>
          <h3>{{ c.name }}</h3>
          <div class="muted small"><Icon name="pin" :size="14" /> {{ c.city }}<template v-if="c.address"> · {{ c.address }}</template></div>
        </div>
      </article>
    </div>
  </template>
</template>

<style scoped>


.hspace { display: none; }
.toolbar { display: flex; gap: 14px; align-items: center; flex-wrap: wrap; margin-bottom: 20px; }
.search { display: flex; align-items: center; gap: 8px; color: var(--muted); background: var(--surface-2); border: 1px solid var(--border); border-radius: 10px; padding: 0 12px; min-width: 260px; }
.search input { border: 0; padding: 8px 0; background: none; box-shadow: none !important; }
.search:focus-within { border-color: var(--primary); box-shadow: 0 0 0 3px rgb(59 130 246 / 25%); }
.chips { display: flex; gap: 8px; flex-wrap: wrap; }
.chips button { display: inline-flex; align-items: center; gap: 6px; font: inherit; padding: 4px 14px; border-radius: 999px; border: 1px solid var(--border); background: transparent; color: var(--muted); cursor: pointer; transition: all .15s; }
.chips button:hover { color: var(--text); border-color: #34405a; }
.chips button.on { background: var(--primary-soft); border-color: #1f3a68; color: #9cc2ff; }

.grid { display: grid; gap: 24px 18px; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); }
.movie { color: inherit; display: grid; gap: 2px; align-content: start; }
.movie:hover { text-decoration: none; }
.movie h3 { margin: 10px 0 0; transition: color .15s; }
.movie:hover h3 { color: var(--sky); }
.pwrap { position: relative; border-radius: 12px; overflow: hidden; border: 1px solid var(--border); transition: border-color .15s; }
.movie:hover .pwrap { border-color: var(--primary); }
.dur { position: absolute; top: 8px; inset-inline-start: 8px; background: rgb(0 0 0 / 65%); color: #fff; font-size: .75rem; padding: 0 8px; border-radius: 6px; }
.agepos { position: absolute; top: 8px; inset-inline-end: 8px; backdrop-filter: blur(4px); }
.gtxt { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; min-width: 0; }
.movie { min-width: 0; }
.genre { min-width: 0; display: flex; align-items: center; gap: 5px; }
.slots { color: var(--sky); }
.slots.none { color: var(--warn); }

.cinemas { display: grid; gap: 12px; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); }
.cinema { display: flex; gap: 14px; align-items: center; }
.cinema h3 { margin: 0; }
.ico { width: 44px; height: 44px; border-radius: 10px; display: grid; place-items: center; background: var(--primary-soft); color: var(--sky); flex: none; }


@media (max-width: 720px) { .hspace { display: block; height: 62px; } }
@media (max-width: 800px) {
  .grid { grid-template-columns: repeat(2, 1fr); gap: 18px 12px; }
  .section-title { margin: 22px 0 12px; }
  /* فیلتر ژانر: یک ردیف قابل‌کشیدن به‌جای چند ردیف */
  .toolbar { gap: 10px; margin-bottom: 14px; }
  .search { min-width: 0; width: 100%; }
  .chips { flex-wrap: nowrap; overflow-x: auto; width: calc(100% + 32px); margin-inline: -16px; padding-inline: 16px; scrollbar-width: none; }
  .chips::-webkit-scrollbar { display: none; }
  .chips button { flex: none; }
}
</style>
