<script setup>
import { computed, ref, nextTick, onMounted, onBeforeUnmount } from 'vue'
import Icon from './Icon.vue'
import SeatRowStrip from './SeatRowStrip.vue'
import { money, num } from '../lib/format'
import { faDigits } from '../lib/seat'
import { SEAT_STATUS, SEAT_HINT, seatStatus } from '../lib/seats'

const props = defineProps({
  rows: { type: Array, required: true },
  mode: { type: String, default: 'view' },
  selected: { type: Array, default: () => [] }, // کلیدهای "rowId:number"
  legend: { type: Boolean, default: true },
  compact: { type: Boolean, default: false },
  zoomable: { type: Boolean, default: false },
})
const emit = defineEmits(['toggle'])

// همهٔ صندلی‌های آزاد یک رنگ دارند؛ قیمت هر صندلی فقط در تولتیپ نشان داده می‌شود.
const tierOf = () => 1
const rid = (r) => r.id ?? r._k

// اندازهٔ صندلی بر اساس عرض موجود و پهن‌ترین ردیف محاسبه می‌شود تا روی موبایل بدون اسکرول جا شود.
const cols = computed(() => Math.max(1, ...props.rows.map((r) => (r.offset || 0) + (r.seatCount || 0))))
const aisles = computed(() => Math.max(0, ...props.rows.map((r) => r.aisleAfter?.length ?? 0)))

const selectedByRow = computed(() => {
  const m = {}
  for (const k of props.selected) {
    const [r, n] = k.split(':')
    ;(m[r] ??= []).push(Number(n))
  }
  return m
})

// شمارش وضعیت‌ها برای راهنما
const counts = computed(() => {
  const c = { free: 0, selected: props.selected.length, held: 0, taken: 0, disabled: 0 }
  for (const r of props.rows) {
    c.taken += r.taken?.length ?? 0
    c.held += r.held?.length ?? 0
    c.disabled += r.disabled?.length ?? 0
  }
  const total = props.rows.reduce((s, r) => s + (r.seatCount || 0), 0)
  c.free = Math.max(0, total - c.taken - c.held - c.disabled - c.selected)
  return c
})

// ---------- زوم ----------
// اندازهٔ پایهٔ صندلی طوری است که ردیف پهن‌ترین سطر در عرض جا شود؛ زوم آن را بزرگ/کوچک می‌کند.
const ZMIN = 0.55, ZMAX = 2.4
const zoom = ref(1)
const cw = ref(0)
const scroller = ref(null)
const baseSeat = computed(() => {
  const raw = (cw.value - 64 - aisles.value * 22 - cols.value * 4) / cols.value
  return Math.max(18, Math.min(30, Number.isFinite(raw) ? raw : 26))
})
const seatPx = computed(() => (props.zoomable && cw.value ? `${(baseSeat.value * zoom.value).toFixed(1)}px` : undefined))

function setZoom(z) {
  const el = scroller.value
  const center = el && el.scrollWidth ? (el.scrollLeft + el.clientWidth / 2) / el.scrollWidth : 0.5
  zoom.value = Math.max(ZMIN, Math.min(ZMAX, Math.round(z * 100) / 100))
  hideTip()
  nextTick(() => { if (el) el.scrollLeft = center * el.scrollWidth - el.clientWidth / 2 })
}
const zoomIn = () => setZoom(zoom.value + 0.25)
const zoomOut = () => setZoom(zoom.value - 0.25)

// دو انگشت: pinch
let pinch = null
const dist = (t) => Math.hypot(t[0].clientX - t[1].clientX, t[0].clientY - t[1].clientY)
function onTouchStart(e) { if (props.zoomable && e.touches.length === 2) pinch = { d: dist(e.touches), z: zoom.value } }
function onTouchMove(e) {
  if (!pinch || e.touches.length !== 2) return
  e.preventDefault()
  setZoom(pinch.z * (dist(e.touches) / pinch.d))
}
const onTouchEnd = (e) => { if (e.touches.length < 2) pinch = null }

let ro
onMounted(() => {
  ro = new ResizeObserver(([e]) => (cw.value = e.contentRect.width))
  ro.observe(root.value)
  const el = scroller.value
  el.addEventListener('touchstart', onTouchStart, { passive: true })
  el.addEventListener('touchmove', onTouchMove, { passive: false })
  el.addEventListener('touchend', onTouchEnd, { passive: true })
})
onBeforeUnmount(() => ro?.disconnect())

// ---------- تولتیپ ----------
// ماوس: با هاور؛ لمسی: با هر ضربه چند ثانیه نمایش داده می‌شود.
const root = ref(null)
const tip = ref(null) // { rowId, n, x, y, below }
let timer
const canHover = () => window.matchMedia?.('(hover: hover)').matches ?? true

function showTip(row, n, el, autoHide) {
  const r = el.getBoundingClientRect()
  const m = root.value.getBoundingClientRect()
  const half = 100
  const x = Math.max(half, Math.min(m.width - half, r.left - m.left + r.width / 2))
  const below = r.top - m.top < 96
  tip.value = { rowId: rid(row), n, x, y: below ? r.bottom - m.top + 10 : r.top - m.top - 10, below }
  clearTimeout(timer)
  if (autoHide) timer = setTimeout(() => (tip.value = null), 3200)
}
const hideTip = () => { clearTimeout(timer); tip.value = null }

const tipData = computed(() => {
  if (!tip.value) return null
  const row = props.rows.find((r) => rid(r) === tip.value.rowId)
  if (!row) return null
  const status = seatStatus(row, tip.value.n, new Set(selectedByRow.value[rid(row)] ?? []))
  return { row, n: tip.value.n, status }
})

function onPick(row, n, el) {
  showTip(row, n, el, !canHover())
  const status = seatStatus(row, n, new Set(selectedByRow.value[rid(row)] ?? []))
  if (props.mode === 'select' && (status === 'free' || status === 'selected')) emit('toggle', row, n)
}

function outside(e) { if (!e.target.closest?.('.seat')) hideTip() }
onMounted(() => document.addEventListener('pointerdown', outside))
onBeforeUnmount(() => { document.removeEventListener('pointerdown', outside); clearTimeout(timer) })
</script>

<template>
  <div ref="root" class="seatmap" :class="{ compact }">
    <div class="screen" aria-hidden="true"><span>پرده نمایش</span></div>

    <div v-if="!rows.length" class="empty">هنوز ردیفی برای این سالن تعریف نشده است.</div>

    <div v-if="zoomable" class="zoombar" role="group" aria-label="بزرگ‌نمایی نقشه">
      <button type="button" aria-label="کوچک‌نمایی" :disabled="zoom <= ZMIN" @click="zoomOut"><Icon name="zoomOut" :size="18" /></button>
      <button type="button" class="pct" aria-label="اندازهٔ اصلی" @click="setZoom(1)">{{ num(Math.round(zoom * 100)) }}٪</button>
      <button type="button" aria-label="بزرگ‌نمایی" :disabled="zoom >= ZMAX" @click="zoomIn"><Icon name="zoomIn" :size="18" /></button>
    </div>

    <div ref="scroller" class="scroller" :class="{ zoomable }" @scroll.passive="hideTip">
      <div class="rows" :style="{ '--cols': cols, '--aisles': aisles, '--sm-seat': seatPx }">
        <SeatRowStrip
          v-for="r in rows"
          :key="rid(r)"
          :row="r"
          :mode="mode"
          :tier="tierOf(r)"
          :selected="selectedByRow[rid(r)] ?? []"
          @pick="(n, el) => onPick(r, n, el)"
          @hover="(n, el) => showTip(r, n, el, false)"
          @leave="hideTip"
        />
      </div>
    </div>

    <Transition name="tip">
      <div v-if="tipData" class="tip" :class="[{ below: tip.below }, tipData.status]" :style="{ left: tip.x + 'px', top: tip.y + 'px' }" role="status">
        <div class="seatname">ردیف {{ faDigits(tipData.row.label) }} · صندلی {{ num(tipData.n) }}</div>
        <div class="state"><i class="dot" />{{ SEAT_STATUS[tipData.status] }}</div>
        <div v-if="tipData.status !== 'disabled'" class="price">{{ money(tipData.row.price) }}</div>
        <div v-if="mode === 'select'" class="hint">{{ SEAT_HINT[tipData.status] }}</div>
      </div>
    </Transition>

    <ul v-if="legend" class="legend">
      <li><i class="sw t1" />آزاد<b v-if="mode === 'select'">{{ num(counts.free) }}</b></li>
      <template v-if="mode === 'select'">
        <li><i class="sw sel" />انتخاب تو<b v-if="counts.selected">{{ num(counts.selected) }}</b></li>
        <li><i class="sw held" />در حال رزرو<b v-if="counts.held">{{ num(counts.held) }}</b></li>
        <li><i class="sw tk" />رزرو شده<b v-if="counts.taken">{{ num(counts.taken) }}</b></li>
      </template>
      <li v-if="counts.disabled"><i class="sw off" />غیرفعال<b>{{ num(counts.disabled) }}</b></li>
    </ul>
  </div>
</template>

<style scoped>
.seatmap { position: relative; container-type: inline-size; }
.screen {
  margin: 0 auto 26px; width: min(420px, 80%); text-align: center; color: var(--muted); font-size: .78rem; letter-spacing: .04em; padding: 6px 0 0;
  border-top: 3px solid var(--primary); border-radius: 50% 50% 0 0 / 100% 100% 0 0;
  background: linear-gradient(180deg, rgb(59 130 246 / 14%), transparent);
}
.seatmap.compact { --sm-seat: 18px; }
.scroller { direction: ltr; overflow-x: auto; padding: 8px 0; }
.scroller.zoomable { touch-action: pan-x pan-y; }
/* همهٔ سطرها هم‌عرض و هم‌تراز؛ شمارهٔ n در همهٔ ردیف‌ها دقیقاً زیر هم است */
.rows { display: grid; gap: 8px; width: max-content; margin-inline: auto; justify-items: stretch; }
.zoombar { display: flex; justify-content: center; align-items: center; gap: 4px; margin: -8px auto 12px; width: max-content; padding: 3px; border-radius: 12px; background: var(--surface-2); border: 1px solid var(--border); }
.zoombar button { display: grid; place-items: center; min-width: 38px; height: 34px; padding: 0 8px; border: 0; border-radius: 9px; background: transparent; color: var(--text); font: inherit; font-size: .82rem; cursor: pointer; }
.zoombar button:hover:not(:disabled) { background: var(--primary-soft); }
.zoombar button:disabled { opacity: .35; cursor: default; }
.zoombar .pct { min-width: 56px; color: var(--muted); font-variant-numeric: tabular-nums; }

.tip {
  position: absolute; z-index: 5; pointer-events: none; min-width: 150px; padding: 8px 12px; border-radius: 10px;
  background: #0b0d12; border: 1px solid #2c3547; box-shadow: 0 8px 24px rgb(0 0 0 / 55%);
  transform: translate(-50%, -100%); font-size: .82rem; line-height: 1.55; text-align: start;
}
.tip.below { transform: translate(-50%, 0); }
.tip::after {
  content: ''; position: absolute; left: 50%; width: 9px; height: 9px; background: #0b0d12; border: 1px solid #2c3547;
  border-width: 0 1px 1px 0; transform: translateX(-50%) rotate(45deg); bottom: -5px;
}
.tip.below::after { bottom: auto; top: -5px; border-width: 1px 0 0 1px; }
.seatname { font-weight: 700; color: var(--text); }
.state { display: flex; align-items: center; gap: 6px; color: var(--muted); }
.dot { width: 8px; height: 8px; border-radius: 50%; background: #5a7bb5; }
.tip.selected .dot { background: var(--primary); }
.tip.held .dot { background: #2dd4bf; }
.tip.taken .dot { background: #3a4150; }
.tip.disabled .dot { background: transparent; border: 1.5px dotted #6b7587; }
.tip.free .dot { background: #5a7bb5; }
.price { color: var(--sky); font-weight: 500; }
.hint { margin-top: 2px; padding-top: 4px; border-top: 1px solid var(--border); color: var(--muted); font-size: .75rem; }
.tip-enter-active, .tip-leave-active { transition: opacity .12s; }
.tip-enter-from, .tip-leave-to { opacity: 0; }

.legend { list-style: none; display: flex; flex-wrap: wrap; gap: 8px 18px; justify-content: center; margin: 14px 0 0; padding: 0; font-size: .85rem; color: var(--muted); }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.legend b { font-weight: 500; color: var(--text); background: var(--surface-2); border-radius: 999px; padding: 0 8px; font-size: .75rem; }
.sw { width: 16px; height: 16px; border-radius: 5px; border: 1.5px solid; display: inline-block; flex: none; }
.sw.t0 { border-color: #3d4c66; background: #1a2332; }
.sw.t1 { border-color: #3b82f6; background: #14274a; }
.sw.t2 { border-color: #6a74f0; background: #1b1f4a; }
.sw.t3 { border-color: #c79020; background: #34270b; }
.sw.sel { border-color: var(--primary); background: var(--primary); }
.sw.held { border: 1.5px dashed #2dd4bf; background: #0d2625; }
.sw.tk { border-color: #1a202b; background: linear-gradient(45deg, transparent 42%, #2a3342 42% 58%, transparent 58%), linear-gradient(-45deg, transparent 42%, #2a3342 42% 58%, transparent 58%), #0e1117; }
.sw.off { border: 1.5px dotted #3a4150; background: transparent; }
</style>
