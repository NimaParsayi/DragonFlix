<script setup>
import { computed, ref, watch } from 'vue'
import Icon from './Icon.vue'
import { hueOf } from '../lib/genre'

const props = defineProps({
  movie: { type: Object, required: true },
  showTitle: { type: Boolean, default: true },
})
const hue = computed(() => hueOf(props.movie.title))
// اگر آدرس پوستر تصویر معتبر نبود (مثلاً لینک یک صفحه)، به پوستر گرافیکی برمی‌گردیم.
const broken = ref(false)
const loaded = ref(false)
watch(() => props.movie.posterUrl, () => { broken.value = false; loaded.value = false })
const hasImage = computed(() => !!props.movie.posterUrl && !broken.value)
const style = computed(() => ({ '--h': hue.value }))
</script>

<template>
  <div class="poster" :style="style">
    <span v-if="hasImage && !loaded" class="imgsk" aria-hidden="true" />
    <img v-if="hasImage" class="img" :class="{ ready: loaded }" :src="movie.posterUrl" :alt="movie.title" referrerpolicy="no-referrer" @load="loaded = true" @error="broken = true" />
    <template v-else>
      <span class="glyph" aria-hidden="true"><Icon :genre="movie.genre" size="100%" :stroke="1.3" /></span>
      <span v-if="showTitle" class="title">{{ movie.title }}</span>
    </template>
  </div>
</template>

<style scoped>
.poster {
  position: relative; overflow: hidden; aspect-ratio: 2 / 3; border-radius: inherit; container-type: inline-size;
  background: linear-gradient(170deg, hsl(var(--h) 45% 24%), hsl(var(--h) 50% 9%));
  display: grid; place-items: center; color: #fff;
}
.img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; opacity: 0; transition: opacity .3s; }
.img.ready { opacity: 1; }
.imgsk { position: absolute; inset: 0; background: linear-gradient(100deg, var(--surface-2) 30%, #1d2535 50%, var(--surface-2) 70%); background-size: 220% 100%; animation: imgsk 1.3s linear infinite; }
@keyframes imgsk { to { background-position: -220% 0; } }
.glyph { width: 34%; aspect-ratio: 1; margin-top: -12%; color: rgb(255 255 255 / 80%); }
.glyph .icon { width: 100%; height: 100%; }
.title {
  position: absolute; inset-inline: 0; bottom: 0; padding: 28px 12px 12px; text-align: center;
  font-weight: 700; font-size: clamp(.95rem, 4cqw, 1.2rem); line-height: 1.3;
  background: linear-gradient(0deg, rgb(0 0 0 / 75%), transparent);
}
</style>
