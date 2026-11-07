<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import Icon from './Icon.vue'
import PosterArt from './PosterArt.vue'
import AgeBadge from './AgeBadge.vue'
import NoteChip from './NoteChip.vue'
import { genreList } from '../lib/genre'
import { num, fmtDate, fmtTime } from '../lib/format'

// اسلایدر صفحهٔ اول. با scroll-snap ساخته شده، پس روی موبایل با کشیدن انگشت می‌چرخد؛
// نقطه‌ها و فلش‌ها و چرخش خودکار هم دارد (با هاور یا لمس متوقف می‌شود).
const props = defineProps({ slides: { type: Array, required: true } })

const track = ref(null)
const index = ref(0)
const paused = ref(false)
const count = computed(() => props.slides.length)
const AUTOPLAY_MS = 6500
let timer, raf, resumeTimer

function go(i) {
  const t = track.value
  if (!t) return
  const n = count.value
  const target = ((i % n) + n) % n
  const dir = getComputedStyle(t).direction === 'rtl' ? -1 : 1 // در راست‌به‌چپ scrollLeft منفی است
  t.scrollTo({ left: dir * target * t.clientWidth, behavior: 'smooth' })
}
function onScroll() {
  cancelAnimationFrame(raf)
  raf = requestAnimationFrame(() => {
    const t = track.value
    if (t) index.value = Math.round(Math.abs(t.scrollLeft) / t.clientWidth)
  })
}

const touchPause = () => { paused.value = true; clearTimeout(resumeTimer); resumeTimer = setTimeout(() => (paused.value = false), 8000) }

onMounted(() => {
  if (count.value < 2 || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
  timer = setInterval(() => { if (!paused.value && !document.hidden) go(index.value + 1) }, AUTOPLAY_MS)
})
onBeforeUnmount(() => { clearInterval(timer); clearTimeout(resumeTimer); cancelAnimationFrame(raf) })
</script>

<template>
  <section class="slider" aria-roledescription="carousel" aria-label="فیلم‌های ویژه" @mouseenter="paused = true" @mouseleave="paused = false" @pointerdown="touchPause">
    <div ref="track" class="track" @scroll.passive="onScroll">
      <article v-for="(s, i) in slides" :key="s.id" class="slide" :aria-label="`${i + 1} از ${count}`">
        <div v-if="s.movie.backdropUrl" class="bg" :style="{ backgroundImage: `url(${s.movie.backdropUrl})` }" aria-hidden="true" />
        <div class="shade" aria-hidden="true" />

        <div class="content">
          <RouterLink class="art" :to="{ name: 'movie', params: { id: s.movie.id } }" :aria-label="s.movie.title" tabindex="-1">
            <PosterArt :movie="s.movie" :show-title="false" />
          </RouterLink>

          <div class="text">
            <NoteChip :note="s.note" />
            <h2>{{ s.movie.title }}</h2>
            <div class="meta">
              <Icon :genre="s.movie.genre" :size="16" /> {{ genreList(s.movie.genre).join(' · ') }}
              <span class="dm">· {{ num(s.movie.durationMinutes) }} دقیقه <AgeBadge :code="s.movie.ageRating" /></span>
            </div>
            <p v-if="s.movie.description" class="desc">{{ s.movie.description }}</p>
            <div class="row">
              <RouterLink class="btn lg" :to="{ name: 'movie', params: { id: s.movie.id } }"><Icon name="ticket" :size="19" /> رزرو بلیت</RouterLink>
              <span v-if="s.nextStartsAt" class="next">نزدیک‌ترین سانس: <b>{{ fmtDate(s.nextStartsAt) }} · {{ fmtTime(s.nextStartsAt) }}</b></span>
            </div>
          </div>
        </div>
      </article>
    </div>

    <template v-if="count > 1">
      <button class="nav prev" type="button" aria-label="اسلاید قبلی" @click="go(index - 1)"><Icon name="back" :size="20" /></button>
      <button class="nav next-btn" type="button" aria-label="اسلاید بعدی" @click="go(index + 1)"><Icon name="back" :size="20" style="transform: rotate(180deg)" /></button>
      <div class="dots" role="tablist">
        <button v-for="(s, i) in slides" :key="s.id" type="button" role="tab" :aria-selected="i === index" :aria-label="`اسلاید ${i + 1}`" :class="{ on: i === index }" @click="go(i)" />
      </div>
    </template>
  </section>
</template>

<style scoped>
.slider { position: relative; border-radius: 16px; overflow: hidden; border: 1px solid var(--border); background: var(--surface); }
.track { display: flex; overflow-x: auto; scroll-snap-type: x mandatory; overscroll-behavior-x: contain; scrollbar-width: none; }
.track::-webkit-scrollbar { display: none; }
.slide { position: relative; flex: 0 0 100%; scroll-snap-align: start; scroll-snap-stop: always; isolation: isolate; min-height: 340px; display: grid; align-items: center; }
.bg { position: absolute; inset: 0; z-index: -2; background-size: cover; background-position: center; }
.shade { position: absolute; inset: 0; z-index: -1; background: linear-gradient(0deg, rgb(11 13 18 / 92%), rgb(11 13 18 / 58%)); }

.content { display: grid; grid-template-columns: 190px minmax(0, 1fr); gap: 34px; align-items: center; padding: 34px 56px 52px; }
.art { display: block; border-radius: 12px; overflow: hidden; border: 1px solid rgb(255 255 255 / 14%); transition: border-color .15s; }
.art:hover { border-color: var(--primary); }
.text { display: grid; gap: 10px; justify-items: start; min-width: 0; }
h2 { margin: 0; font-size: 2.1rem; }
.meta { color: var(--muted); display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.desc { max-width: 58ch; margin: 0; color: #c3cad6; display: -webkit-box; -webkit-line-clamp: 3; line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
.row { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; margin-top: 4px; }
.next { color: var(--muted); font-size: .9rem; }
.next b { color: var(--text); }
.dm { white-space: nowrap; display: inline-flex; align-items: center; gap: 6px; }

.nav { position: absolute; top: 50%; transform: translateY(-50%); width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; cursor: pointer;
  color: #fff; background: rgb(0 0 0 / 45%); border: 1px solid rgb(255 255 255 / 18%); backdrop-filter: blur(4px); transition: background .15s; }
.nav:hover { background: rgb(0 0 0 / 70%); }
.prev { inset-inline-end: 12px; }
.next-btn { inset-inline-start: 12px; }

.dots { position: absolute; bottom: 14px; inset-inline: 0; display: flex; justify-content: center; gap: 7px; }
.dots button { width: 8px; height: 8px; padding: 0; border: 0; border-radius: 999px; background: rgb(255 255 255 / 30%); cursor: pointer; transition: width .2s, background .2s; }
.dots button.on { width: 24px; background: var(--sky); }

@media (max-width: 800px), (hover: none) { .nav { display: none; } }

@media (max-width: 800px) {
  /* موبایل: کارت کوتاه؛ توضیح ویژه می‌ماند ولی توضیح بلند حذف می‌شود */
  .slide { min-height: 0; }
  .content { grid-template-columns: 124px minmax(0, 1fr); gap: 14px; padding: 14px 14px 34px; }
  .art { border-radius: 12px; }
  .text { gap: 5px; }
  h2 { font-size: 1.02rem; line-height: 1.5; }
  .text :deep(.notechip) { font-size: .72rem; padding: 1px 9px; gap: 5px; }
  .desc, .next { display: none; }
  .meta { display: block; line-height: 1.8; font-size: .78rem; }
  .btn.lg { padding: 5px 14px; font-size: .82rem; border-radius: 9px; margin-top: 2px; }
  .dots { bottom: 10px; }
}

/* موبایل: اسلایدر لبه‌تا‌لبه و زیر هدر شفاف؛ با هدر یک بلوک می‌شود */
@media (max-width: 720px) {
  .slider { width: 100vw; margin-inline: calc(50% - 50vw); border: 0; border-radius: 0 0 26px 26px; }
  .shade { background: linear-gradient(0deg, rgb(11 13 18 / 94%), rgb(11 13 18 / 55%) 55%, rgb(11 13 18 / 70%)); }
  .content { padding-top: calc(72px + var(--safe-top)); padding-bottom: 40px; }
  .dots { bottom: 14px; }
}
</style>
