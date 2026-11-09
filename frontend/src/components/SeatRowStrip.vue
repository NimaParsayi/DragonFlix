<script setup>
import { computed } from 'vue'
import { SEAT_STATUS, seatStatus } from '../lib/seats'
import { faDigits } from '../lib/seat'
import { num } from '../lib/format'

// mode: 'select' (رزرو)، 'view' (فقط نمایش)، 'edit' (طراح چینش: کلیک روی صندلی = غیرفعال/فعال، کلیک روی فاصله = راهرو)
const props = defineProps({
  row: { type: Object, required: true },
  mode: { type: String, default: 'view' },
  tier: { type: Number, default: 0 },
  selected: { type: Array, default: () => [] },
})
const emit = defineEmits(['pick', 'hover', 'leave', 'toggle-aisle', 'toggle-disabled'])

const sel = computed(() => new Set(props.selected))
const aisles = computed(() => new Set(props.row.aisleAfter ?? []))
const count = computed(() => Math.max(0, Math.min(60, props.row.seatCount || 0)))
const offset = computed(() => Math.max(0, props.row.offset || 0))
const state = (n) => seatStatus(props.row, n, sel.value)

function click(n, e) {
  if (props.mode === 'edit') emit('toggle-disabled', n)
  else emit('pick', n, e.currentTarget)
}
const enter = (n, e) => { if (e.pointerType === 'mouse' && props.mode !== 'edit') emit('hover', n, e.currentTarget) }
const leave = (e) => { if (e.pointerType === 'mouse') emit('leave') }
const focus = (n, e) => { if (props.mode !== 'edit' && e.currentTarget.matches(':focus-visible')) emit('hover', n, e.currentTarget) }
</script>

<template>
  <div class="strip" :class="`t${Math.min(tier, 3)}`">
    <span class="label">{{ faDigits(row.label) }}</span>
    <div class="seats">
      <span class="indent" :style="{ width: `calc(var(--seat-step) * ${offset})` }" />
      <template v-for="n in count" :key="n">
        <button
          type="button"
          class="seat"
          :class="[state(n), { edit: mode === 'edit', view: mode === 'view' }]"
          :aria-label="`ردیف ${faDigits(row.label)} صندلی ${num(n)} — ${SEAT_STATUS[state(n)]}`"
          :aria-pressed="mode === 'select' ? state(n) === 'selected' : undefined"
          :aria-disabled="mode === 'select' && !['free', 'selected'].includes(state(n))"
          :title="mode === 'edit' ? (state(n) === 'disabled' ? 'فعال‌کردن صندلی' : 'غیرفعال‌کردن صندلی') : undefined"
          @click="click(n, $event)"
          @pointerenter="enter(n, $event)"
          @pointerleave="leave"
          @focus="focus(n, $event)"
          @blur="emit('leave')"
        >{{ num(n).replace(/٬/g, '') }}</button>
        <template v-if="n < count">
          <button
            v-if="mode === 'edit'"
            type="button"
            class="gap editable"
            :class="{ aisle: aisles.has(n) }"
            :aria-label="aisles.has(n) ? `حذف راهرو بعد از صندلی ${n}` : `افزودن راهرو بعد از صندلی ${n}`"
            :title="aisles.has(n) ? 'حذف راهرو' : 'افزودن راهرو'"
            @click="emit('toggle-aisle', n)"
          />
          <span v-else class="gap" :class="{ aisle: aisles.has(n) }" />
        </template>
      </template>
    </div>
    <span class="label end">{{ faDigits(row.label) }}</span>
  </div>
</template>

<style scoped>
.strip {
  --seat: var(--sm-seat, clamp(22px, calc((100cqw - 64px - var(--aisles, 0) * 22px - var(--cols, 10) * 4px) / var(--cols, 10)), 28px));
  --seat-step: calc(var(--seat) + 4px);
  display: flex; align-items: center; gap: 10px; direction: rtl; width: 100%;
}
.label { width: 20px; text-align: center; font-weight: 700; color: var(--muted); font-size: .85rem; }
.seats { display: flex; align-items: center; flex: 1; }
.indent { display: inline-block; height: 1px; flex: none; }

.seat {
  position: relative; width: var(--seat); height: var(--seat); margin: 0 2px; flex: none; padding: 0;
  border-radius: 8px 8px 5px 5px; border: 1.5px solid var(--c, #3d4c66);
  background: var(--bg-c, #1a2332); color: var(--ink, #9fb3d1);
  font-family: inherit; font-weight: 600; font-size: calc(var(--seat) * .4); line-height: 1;
  cursor: pointer; transition: background .12s, border-color .12s, color .12s, transform .12s;
}
.seat.view { cursor: default; }

/* آزاد */
.seat.free:hover, .seat.free:focus-visible { background: var(--c); color: #fff; transform: translateY(-2px); }
.seat.view.free:hover { transform: none; }
/* انتخاب شما */
.seat.selected { background: var(--primary); border-color: var(--primary); color: #fff; }
.seat.selected:hover { background: var(--primary-hover); }
/* در حال رزرو توسط دیگری */
.seat.held { background: #0d2625; border: 1.5px dashed #2dd4bf; color: #2dd4bf; opacity: .85; cursor: not-allowed; }
/* رزرو شده: ضربدر */
.seat.taken {
  background:
    linear-gradient(45deg, transparent 45%, #2a3342 45% 55%, transparent 55%),
    linear-gradient(-45deg, transparent 45%, #2a3342 45% 55%, transparent 55%), #0e1117;
  background-size: 60% 60%; background-repeat: no-repeat; background-position: center;
  border-color: #1a202b; color: transparent; cursor: not-allowed;
}
/* غیرفعال: خط‌چین با یک خط تیره */
.seat.disabled { background: transparent; border: 1.5px dotted #3a4150; color: transparent; cursor: not-allowed; }
.seat.disabled::after { content: ''; position: absolute; inset-inline: 30%; top: calc(50% - 1px); height: 2px; border-radius: 1px; background: #3a4150; }
.seat.edit { cursor: pointer; }
.seat.edit.disabled:hover { border-color: var(--sky); }

.gap { width: 0; height: var(--seat); flex: none; padding: 0; border: 0; background: none; }
.gap.aisle { width: 22px; }
.gap.editable { width: 10px; cursor: pointer; border-radius: 4px; position: relative; }
.gap.editable::after { content: ''; position: absolute; inset: 6px 3px; border-radius: 2px; background: var(--border); }
.gap.editable:hover::after { background: var(--sky); }
.gap.editable.aisle { width: 26px; }
.gap.editable.aisle::after { background: repeating-linear-gradient(90deg, var(--primary) 0 3px, transparent 3px 6px); inset: 12px 2px; height: 3px; }

.t0 { --c: #3d4c66; --bg-c: #1a2332; --ink: #9fb3d1; }
.t1 { --c: #3b82f6; --bg-c: #14274a; --ink: #bcd4ff; }
.t2 { --c: #6a74f0; --bg-c: #1b1f4a; --ink: #c9ceff; }
.t3 { --c: #c79020; --bg-c: #34270b; --ink: #f2d089; }


</style>
