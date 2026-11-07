<script setup>
import Skeleton from '../components/Skeleton.vue'
import Icon from '../components/Icon.vue'
import { ref, computed, onMounted, watch } from 'vue'
import PosterArt from '../components/PosterArt.vue'
import { api } from '../lib/api'
import { toast } from '../lib/ui'
import * as tg from '../lib/tg'
import AgeBadge from '../components/AgeBadge.vue'
import NoteChip from '../components/NoteChip.vue'
import { genreList } from '../lib/genre'
import { num, money, fmtDate, fmtTime, dayKey } from '../lib/format'

const props = defineProps({ id: String })
const data = ref(null)
const error = ref('')
const cinemaFilter = ref('')
const day = ref('')
const following = ref(false)
const followBusy = ref(false)

onMounted(async () => {
  try {
    data.value = await api.get(`/api/movies/${props.id}`)
    following.value = !!data.value.following
  } catch (e) {
    error.value = e.message
  }
})

// دنبال‌کردن فیلم: از سانس‌های جدیدش در تلگرام خبر می‌گیرد
async function share() {
  const m = data.value.movie
  const url = `${tg.botLink}?start=movie_${m.id}`
  const r = await tg.shareLink(url, `🎬 ${m.title}${m.specialNote ? ' — ' + m.specialNote : ''}\nبلیت این فیلم رو از دراگون‌فلیکس رزرو کن`)
  tg.haptic.tap()
  if (r === 'copied') toast('لینک فیلم کپی شد.')
  else if (r === 'failed') toast('کپی نشد؛ دوباره امتحان کن.', 'error')
}

async function toggleFollow() {
  followBusy.value = true
  try {
    await (following.value ? api.del(`/api/movies/${props.id}/follow`) : api.post(`/api/movies/${props.id}/follow`))
    following.value = !following.value
    tg.haptic.success()
    toast(following.value ? 'دنبالش کردی؛ سانس‌های جدید رو توی تلگرام خبر می‌دیم.' : 'دیگه این فیلم رو دنبال نمی‌کنی.')
  } catch (e) { toast(e.message, 'error') } finally { followBusy.value = false }
}

const cinemas = computed(() => {
  const m = new Map()
  for (const s of data.value?.showtimes ?? []) m.set(s.cinemaId, s.cinemaName)
  return [...m].map(([id, name]) => ({ id, name }))
})

const visible = computed(() =>
  (data.value?.showtimes ?? []).filter((s) => !cinemaFilter.value || s.cinemaId === Number(cinemaFilter.value)),
)
const days = computed(() => {
  const m = new Map()
  for (const s of visible.value) if (!m.has(dayKey(s.startsAt))) m.set(dayKey(s.startsAt), s.startsAt)
  return [...m].map(([key, iso]) => ({ key, label: fmtDate(iso) }))
})
watch(days, (d) => { if (!d.some((x) => x.key === day.value)) day.value = d[0]?.key ?? '' }, { immediate: true })

const byCinema = computed(() => {
  const m = new Map()
  for (const s of visible.value.filter((x) => dayKey(x.startsAt) === day.value)) {
    if (!m.has(s.cinemaId)) m.set(s.cinemaId, { name: s.cinemaName, city: s.city, items: [] })
    m.get(s.cinemaId).items.push(s)
  }
  return [...m.values()]
})
</script>

<template>
  <div v-if="error" class="alert error">{{ error }}</div>
  <Skeleton v-else-if="!data" variant="movie" />

  <template v-else>
    <section class="hero-band head" :class="{ 'has-bg': data.movie.backdropUrl }">
      <div v-if="data.movie.backdropUrl" class="bg" :style="{ backgroundImage: `url(${data.movie.backdropUrl})` }" aria-hidden="true" />
      <div class="art"><PosterArt :movie="data.movie" :show-title="false" /></div>
      <div class="info">
        <div class="row"><span v-for="g in genreList(data.movie.genre)" :key="g" class="badge"><Icon :genre="g" :size="14" /> {{ g }}</span><span class="badge"><Icon name="clock" :size="14" /> {{ num(data.movie.durationMinutes) }} دقیقه</span>
          <AgeBadge :code="data.movie.ageRating" /></div>
        <NoteChip :note="data.movie.specialNote" />
        <h1>{{ data.movie.title }}</h1>
        <div v-if="data.movie.originalTitle" class="orig" dir="ltr">{{ data.movie.originalTitle }}<template v-if="data.movie.releaseYear"> ({{ data.movie.releaseYear }})</template></div>
      </div>
      <div class="rest">
        <p>{{ data.movie.description }}</p>
        <div class="actions">
        <button class="btn sm follow" :class="following ? 'secondary' : ''" :disabled="followBusy" @click="toggleFollow">
          <Icon :name="following ? 'bell' : 'bellOff'" :size="16" /> {{ following ? 'دنبالش می‌کنی' : 'دنبال کردن و خبر سانس جدید' }}
        </button>
        <button class="btn secondary sm" @click="share"><Icon name="share" :size="16" /> اشتراک‌گذاری</button>
        </div>
      </div>
    </section>

    <div class="section-title">
      <h2>انتخاب سانس</h2>
      <span class="grow" />
      <select v-if="cinemas.length > 1" v-model="cinemaFilter" class="cfilter" aria-label="فیلتر سینما">
        <option value="">همهٔ سینماها</option>
        <option v-for="c in cinemas" :key="c.id" :value="c.id">{{ c.name }}</option>
      </select>
    </div>

    <div v-if="!days.length" class="empty card">سانس پیش‌رویی برای این فیلم ثبت نشده است.</div>

    <template v-else>
      <div class="days" role="tablist">
        <button v-for="d in days" :key="d.key" role="tab" :aria-selected="d.key === day" :class="{ on: d.key === day }" @click="day = d.key">
          {{ d.label }}
        </button>
      </div>

      <section v-for="c in byCinema" :key="c.name" class="card cinema">
        <div class="chead"><span class="ico"><Icon name="film" :size="22" /></span><div><h3>{{ c.name }}</h3><span class="muted small"><Icon name="pin" :size="13" /> {{ c.city }}</span></div></div>
        <div class="slots">
          <RouterLink v-for="s in c.items" :key="s.id" :to="{ name: 'booking', params: { id: s.id } }" class="slot">
            <strong>{{ fmtTime(s.startsAt) }}</strong>
            <span class="hall">{{ s.hallName }}</span>
            <span class="price">از {{ money(s.minPrice) }}</span>
          </RouterLink>
        </div>
      </section>
    </template>
  </template>
</template>

<style scoped>
.head { position: relative; overflow: hidden; isolation: isolate; display: grid; grid-template-columns: 170px 1fr; gap: 32px; align-items: center; padding: 28px 36px; }
.bg { position: absolute; inset: 0; z-index: -2; background-size: cover; background-position: center; }
.has-bg::before { content: ''; position: absolute; inset: 0; z-index: -1; background: linear-gradient(0deg, rgb(11 13 18 / 88%), rgb(11 13 18 / 62%)); }
.orig { color: var(--muted); font-size: .9rem; margin-top: -6px; }
.art { grid-row: span 2; align-self: start; border-radius: 12px; overflow: hidden; border: 1px solid var(--border); }
.rest { display: grid; gap: 12px; justify-items: start; }
.info { display: grid; gap: 10px; justify-items: start; }
.info h1 { font-size: 2rem; margin: 0; }
.info p { color: #c3cad6; max-width: 62ch; margin: 0; }
.badge { border: 1px solid var(--border); background: transparent; color: var(--muted); padding: 1px 12px; }
.cfilter { width: auto; min-width: 190px; }

.days { display: flex; gap: 8px; overflow-x: auto; padding: 2px 2px 14px; }
.days button { font: inherit; flex: none; padding: 7px 18px; border-radius: 10px; border: 1px solid var(--border); background: var(--surface); color: var(--muted); cursor: pointer; transition: all .15s; }
.days button:hover { color: var(--text); border-color: #34405a; }
.days button.on { background: var(--primary); border-color: var(--primary); color: #fff; }

.cinema { margin-bottom: 12px; }
.chead { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; }
.chead h3 { margin: 0; }
.ico { color: var(--sky); width: 40px; height: 40px; border-radius: 10px; display: grid; place-items: center; background: var(--primary-soft); }
.slots { display: flex; flex-wrap: wrap; gap: 10px; }
.slot { display: grid; text-align: center; padding: 8px 20px; min-width: 128px; color: var(--text); border: 1px solid var(--border); border-radius: 10px; background: var(--surface-2); transition: border-color .15s, background .15s; }
.slot strong { font-size: 1.25rem; color: var(--sky); line-height: 1.4; }
.slot .hall { font-size: .82rem; }
.slot .price { font-size: .78rem; color: var(--muted); }
.slot:hover { text-decoration: none; border-color: var(--primary); background: var(--primary-soft); }

@media (max-width: 720px) {
  .head { grid-template-columns: 104px 1fr; padding: 18px; gap: 14px 16px; align-items: start; }
  .art { grid-row: auto; }
  .rest { grid-column: 1 / -1; }
  .rest p { font-size: .95rem; line-height: 1.9; }
  .info h1 { font-size: 1.4rem; }
}
.actions { display: flex; flex-wrap: wrap; gap: 8px; }
</style>
