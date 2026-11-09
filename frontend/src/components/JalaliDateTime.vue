<script setup>
// انتخاب‌گر تاریخ و ساعتِ شمسی؛ مقدار v-model همان قالب datetime-local میلادی است («2026-11-10T10:10»)
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { toJalali, toGregorian, jalaliMonthLength, jalaliWeekday, MONTHS } from '../lib/jalali'
import { num } from '../lib/format'
import Icon from './Icon.vue'

const props = defineProps({ modelValue: { type: String, default: '' }, required: Boolean })
const emit = defineEmits(['update:modelValue'])
const pad = (n) => String(n).padStart(2, '0')
const fa = (n) => num(n).replace(/٬/g, '')

const parsed = computed(() => {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(props.modelValue || '')
  if (!m) return null
  const [gy, gm, gd, h, mi] = m.slice(1).map(Number)
  return { ...toJalali(gy, gm, gd), h, mi }
})

const open = ref(false)
const view = ref({ jy: 1405, jm: 1 })
const root = ref(null)

function syncView() {
  const p = parsed.value
  if (p) view.value = { jy: p.jy, jm: p.jm }
  else { const n = new Date(); const j = toJalali(n.getFullYear(), n.getMonth() + 1, n.getDate()); view.value = { jy: j.jy, jm: j.jm } }
}
watch(open, (o) => o && syncView())

const label = computed(() => {
  const p = parsed.value
  return p ? `${fa(p.jy)}/${fa(pad(p.jm))}/${fa(pad(p.jd))} — ${fa(pad(p.h))}:${fa(pad(p.mi))}` : 'انتخاب تاریخ و ساعت'
})

const days = computed(() => {
  const { jy, jm } = view.value
  const len = jalaliMonthLength(jy, jm)
  const lead = jalaliWeekday(jy, jm, 1)
  return [...Array(lead).fill(null), ...Array.from({ length: len }, (_, i) => i + 1)]
})
const today = (() => { const n = new Date(); return toJalali(n.getFullYear(), n.getMonth() + 1, n.getDate()) })()
const isSel = (d) => parsed.value && parsed.value.jy === view.value.jy && parsed.value.jm === view.value.jm && parsed.value.jd === d
const isToday = (d) => today.jy === view.value.jy && today.jm === view.value.jm && today.jd === d

function commit(jy, jm, jd, h, mi) {
  const g = toGregorian(jy, jm, jd)
  emit('update:modelValue', `${g.gy}-${pad(g.gm)}-${pad(g.gd)}T${pad(h)}:${pad(mi)}`)
}
function pickDay(d) {
  const p = parsed.value
  commit(view.value.jy, view.value.jm, d, p?.h ?? 18, p?.mi ?? 0)
}
function setTime(h, mi) {
  const p = parsed.value ?? { ...today, h: 18, mi: 0 }
  commit(p.jy, p.jm, p.jd, h, mi)
}
function shift(delta) {
  let { jy, jm } = view.value
  jm += delta
  if (jm > 12) { jm = 1; jy++ } else if (jm < 1) { jm = 12; jy-- }
  view.value = { jy, jm }
}
const goToday = () => { view.value = { jy: today.jy, jm: today.jm }; pickDay(today.jd) }

const hours = Array.from({ length: 24 }, (_, i) => i)
const minutes = computed(() => {
  const s = new Set(Array.from({ length: 12 }, (_, i) => i * 5))
  if (parsed.value) s.add(parsed.value.mi)
  return [...s].sort((a, b) => a - b)
})
const curH = computed({ get: () => parsed.value?.h ?? 18, set: (h) => setTime(h, parsed.value?.mi ?? 0) })
const curM = computed({ get: () => parsed.value?.mi ?? 0, set: (m) => setTime(parsed.value?.h ?? 18, m) })

const outside = (e) => { if (open.value && !root.value?.contains(e.target)) open.value = false }
const onKey = (e) => e.key === 'Escape' && (open.value = false)
onMounted(() => { document.addEventListener('pointerdown', outside); document.addEventListener('keydown', onKey) })
onBeforeUnmount(() => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', onKey) })
</script>

<template>
  <div ref="root" class="jdt">
    <button type="button" class="trigger" :class="{ empty: !parsed }" :aria-expanded="open" @click="open = !open">
      <Icon name="calendar" :size="17" />
      <span>{{ label }}</span>
    </button>
    <!-- برای اعتبارسنجی required مرورگر -->
    <input class="proxy" tabindex="-1" aria-hidden="true" :required="required" :value="modelValue" @focus="open = true" />

    <div v-if="open" class="pop" role="dialog" aria-label="انتخاب تاریخ و ساعت">
      <div class="head">
        <button type="button" class="nav" aria-label="ماه بعد" @click="shift(1)">‹</button>
        <b>{{ MONTHS[view.jm - 1] }} {{ fa(view.jy) }}</b>
        <button type="button" class="nav" aria-label="ماه قبل" @click="shift(-1)">›</button>
      </div>
      <div class="grid wk"><span v-for="w in ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج']" :key="w">{{ w }}</span></div>
      <div class="grid">
        <template v-for="(d, i) in days" :key="i">
          <span v-if="d === null" />
          <button v-else type="button" class="day" :class="{ sel: isSel(d), today: isToday(d) }" @click="pickDay(d)">{{ fa(d) }}</button>
        </template>
      </div>
      <div class="time">
        <span class="muted small">ساعت</span>
        <span class="clock" dir="ltr">
          <select v-model.number="curH" aria-label="ساعت"><option v-for="h in hours" :key="h" :value="h">{{ fa(pad(h)) }}</option></select>
          <b>:</b>
          <select v-model.number="curM" aria-label="دقیقه"><option v-for="m in minutes" :key="m" :value="m">{{ fa(pad(m)) }}</option></select>
        </span>
      </div>
      <div class="foot">
        <button type="button" class="btn secondary sm" @click="goToday">امروز</button>
        <button type="button" class="btn sm" :disabled="!parsed" @click="open = false">تأیید</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.jdt { position: relative; }
.trigger {
  width: 100%; display: flex; align-items: center; gap: 8px; font: inherit; color: var(--text); text-align: start; cursor: pointer;
  padding: 8px 12px; border-radius: 10px; border: 1px solid var(--border); background: var(--surface-2);
}
.trigger.empty { color: #5f6a7d; }
.trigger:focus-visible, .trigger[aria-expanded='true'] { outline: 0; border-color: var(--primary); box-shadow: 0 0 0 3px rgb(59 130 246 / 25%); }
.proxy { position: absolute; inset: auto 0 0 0; height: 1px; opacity: 0; pointer-events: none; padding: 0; border: 0; }
.pop {
  position: absolute; z-index: 60; inset-inline-start: 0; top: calc(100% + 6px); width: min(310px, calc(100vw - 32px));
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px; padding: 12px; box-shadow: 0 14px 40px rgb(0 0 0 / 60%); display: grid; gap: 8px;
}
.head { display: flex; align-items: center; justify-content: space-between; }
.nav { font-size: 1.4rem; line-height: 1; display: grid; place-items: center; width: 32px; height: 32px; border-radius: 8px; border: 0; background: transparent; color: var(--muted); cursor: pointer; }
.nav:hover { background: var(--surface-2); color: var(--text); }
.grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 2px; text-align: center; }
.wk span { font-size: .75rem; color: var(--muted); padding: 2px 0; }
.day { font: inherit; font-size: .9rem; height: 34px; border-radius: 8px; border: 1px solid transparent; background: transparent; color: var(--text); cursor: pointer; }
.day:hover { background: var(--surface-2); }
.day.today { border-color: #1f3a68; color: #9cc2ff; }
.day.sel { background: var(--primary); border-color: var(--primary); color: #fff; font-weight: 700; }
.time { display: flex; align-items: center; justify-content: center; gap: 8px; padding-top: 8px; border-top: 1px solid var(--border); }
.clock { display: inline-flex; align-items: center; gap: 8px; }
.time select { width: auto; padding: 4px 10px; }
.foot { display: flex; justify-content: space-between; }
</style>
