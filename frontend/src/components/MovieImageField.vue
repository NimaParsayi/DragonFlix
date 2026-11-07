<script setup>
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import Icon from './Icon.vue'
import { toast } from '../lib/ui'

// یک تصویر فیلم (کاور یا بک‌دراپ). سه منبع دارد که هر کدام جای قبلی را می‌گیرد:
// فایل آپلودی، تصویر انتخاب‌شده از TMDB (هنگام ذخیره روی سرور ذخیره می‌شود)، یا آدرس تصویر.
const props = defineProps({
  label: String,
  kind: { type: String, default: 'poster' }, // poster | backdrop
  modelValue: { type: Object, required: true }, // { url, tmdbPath, file }
  imageBase: { type: String, default: '' },
})
const emit = defineEmits(['update:modelValue'])

const MAX = { poster: 5, backdrop: 10 } // مگابایت؛ مطابق سرور
const showUrl = ref(false)
const failed = ref(false)
const objectUrl = ref('')

const set = (patch) => emit('update:modelValue', { ...props.modelValue, ...patch })

watch(() => props.modelValue.file, (f) => {
  if (objectUrl.value) URL.revokeObjectURL(objectUrl.value)
  objectUrl.value = f ? URL.createObjectURL(f) : ''
}, { immediate: true })
onBeforeUnmount(() => objectUrl.value && URL.revokeObjectURL(objectUrl.value))

const preview = computed(() => {
  const v = props.modelValue
  if (v.file) return objectUrl.value
  if (v.tmdbPath) return `${props.imageBase}/${props.kind === 'poster' ? 'w342' : 'w780'}${v.tmdbPath}`
  return v.url
})
watch(preview, () => (failed.value = false))

const source = computed(() => {
  const v = props.modelValue
  if (v.file) return `فایل جدید: ${v.file.name}`
  if (v.tmdbPath) return 'از TMDB؛ هنگام ذخیره روی سرور ذخیره می‌شود'
  if (v.url?.startsWith('/uploads/')) return 'ذخیره‌شده روی سرور'
  if (v.url) return 'آدرس بیرونی'
  return ''
})
const has = computed(() => !!preview.value)

function pick(e) {
  const f = e.target.files?.[0]
  e.target.value = ''
  if (!f) return
  if (!/^image\/(jpeg|png|webp)$/.test(f.type)) return toast('فقط تصویر JPG، PNG یا WEBP قابل انتخاب است.', 'error')
  if (f.size > MAX[props.kind] * 1024 * 1024) return toast(`حجم تصویر نباید بیشتر از ${MAX[props.kind]} مگابایت باشد.`, 'error')
  set({ file: f, tmdbPath: '' })
}
const remove = () => { set({ url: '', tmdbPath: '', file: null }); showUrl.value = false }
const typeUrl = (v) => set({ url: v, tmdbPath: '', file: null })
</script>

<template>
  <div class="field-wrap">
    <div class="lbl">{{ label }}</div>
    <div class="box" :class="kind">
      <img v-if="has && !failed" :src="preview" :alt="label" @error="failed = true" />
      <div v-else class="ph">
        <Icon name="film" :size="26" />
        <span v-if="has && failed">پیش‌نمایش بارگذاری نشد</span>
        <span v-else>بدون تصویر</span>
      </div>
    </div>
    <div v-if="failed && modelValue.tmdbPath" class="hint">مرورگر شما به TMDB دسترسی ندارد؛ اگر سرور دسترسی داشته باشد هنگام ذخیره تصویر را می‌گیرد.</div>
    <div v-if="source" class="src">{{ source }}</div>

    <div class="btns">
      <label class="btn secondary sm file"><Icon name="upload" :size="15" /> انتخاب فایل
        <input type="file" accept="image/jpeg,image/png,image/webp" hidden @change="pick" />
      </label>
      <button type="button" class="btn secondary sm" @click="showUrl = !showUrl">آدرس تصویر</button>
      <button v-if="has" type="button" class="btn danger sm" @click="remove"><Icon name="trash" :size="15" /> حذف</button>
    </div>
    <input v-if="showUrl" :value="modelValue.url" dir="ltr" placeholder="https://…/image.jpg" aria-label="آدرس تصویر" @input="typeUrl($event.target.value)" />
  </div>
</template>

<style scoped>
.field-wrap { display: grid; gap: 8px; }
.lbl { font-size: .88rem; color: var(--muted); }
.box { background: var(--surface-2); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; display: grid; place-items: center; }
.box.poster { aspect-ratio: 2 / 3; width: 100%; max-width: 150px; }
.box.backdrop { aspect-ratio: 16 / 9; width: 100%; }
.box img { width: 100%; height: 100%; object-fit: cover; display: block; }
.ph { display: grid; justify-items: center; gap: 4px; color: var(--muted); font-size: .78rem; text-align: center; padding: 8px; }
.src, .hint { font-size: .78rem; color: var(--muted); }
.hint { color: var(--warn); }
.btns { display: flex; gap: 6px; flex-wrap: wrap; }
.file { cursor: pointer; }
</style>
