<script setup>
import { seatLines } from '../lib/seat'
import { ref, computed, watchEffect, onMounted, onBeforeUnmount } from 'vue'
import QRCode from 'qrcode'
import Icon from './Icon.vue'
import PosterArt from './PosterArt.vue'
import { downloadFile } from '../lib/api'
import { fmtDate, fmtTime, fmtCode, money, num } from '../lib/format'

const props = defineProps({ booking: { type: Object, required: true } })
const emit = defineEmits(['close'])

const svg = ref('')
watchEffect(async () => {
  svg.value = await QRCode.toString(props.booking.code, { type: 'svg', margin: 0, errorCorrectionLevel: 'M' })
})

const busy = ref(false)
const err = ref('')
async function pdf() {
  busy.value = true; err.value = ''
  try { await downloadFile(`/api/bookings/${props.booking.id}/pdf`, `ticket-${props.booking.code}.pdf`) }
  catch (e) { err.value = e.message } finally { busy.value = false }
}

const past = computed(() => new Date(props.booking.startsAt) <= new Date())
const inactive = computed(() => past.value || !!props.booking.checkedInAt)
const onKey = (e) => e.key === 'Escape' && emit('close')
onMounted(() => { document.addEventListener('keydown', onKey); document.body.style.overflow = 'hidden' })
onBeforeUnmount(() => { document.removeEventListener('keydown', onKey); document.body.style.overflow = '' })
</script>

<template>
  <Teleport to="body">
    <div class="backdrop" @click.self="emit('close')">
      <div class="ticket" role="dialog" aria-modal="true" :aria-label="`بلیت ${booking.movie}`">
        <button class="x" aria-label="بستن" @click="emit('close')">✕</button>

        <header>
          <div class="mini"><PosterArt :movie="{ title: booking.movie, genre: booking.genre, posterUrl: booking.posterUrl }" :show-title="false" /></div>
          <div>
            <h2>{{ booking.movie }}</h2>
            <div class="muted">{{ booking.cinema }} · {{ booking.hall }}</div>
            <div v-if="booking.buyer?.trim()" class="buyer">نام: <b>{{ booking.buyer.trim() }}</b></div>
          </div>
        </header>

        <div class="when">
          <div><span class="muted small">تاریخ</span><b>{{ fmtDate(booking.startsAt) }}</b></div>
          <div><span class="muted small">ساعت</span><b>{{ fmtTime(booking.startsAt) }}</b></div>
          <div><span class="muted small">مبلغ</span><b>{{ money(booking.totalPrice) }}</b></div>
        </div>

        <div class="seats">
          <span v-for="l in seatLines(booking.seats)" :key="l" class="chip">{{ l }}</span>
        </div>

        <div class="perf" aria-hidden="true" />

        <div class="qrwrap">
          <div class="qr" :class="{ dim: inactive }" v-html="svg" />
          <div class="code" dir="ltr">{{ fmtCode(booking.code) }}</div>
          <p class="muted small" style="margin: 0; text-align: center">
            {{ booking.checkedInAt ? 'این بلیت برای ورود استفاده شده.' : past ? 'این سانس برگزار شده.' : 'موقع ورود این صفحه رو به مسئول سالن نشون بده.' }}
          </p>
        </div>

        <div v-if="err" class="alert error small">{{ err }}</div>
        <div class="actions">
          <button class="btn" :disabled="busy" @click="pdf"><Icon name="download" :size="17" /> {{ busy ? 'در حال آماده‌سازی…' : 'دانلود PDF' }}</button>
          <button class="btn secondary" @click="emit('close')">بستن</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.backdrop { position: fixed; inset: 0; z-index: 100; background: rgb(0 0 0 / 70%); display: grid; place-items: center; padding: 16px; overflow-y: auto; }
.ticket { position: relative; width: min(400px, 100%); background: var(--surface); border: 1px solid var(--border); border-radius: 16px; padding: 22px; display: grid; gap: 14px; margin: auto; }
.x { position: absolute; top: 12px; inset-inline-start: 12px; background: none; border: 0; color: var(--muted); font-size: 1rem; cursor: pointer; width: 30px; height: 30px; border-radius: 8px; }
.x:hover { background: var(--surface-2); color: var(--text); }
header { display: flex; gap: 14px; align-items: center; padding-inline-start: 26px; }
.mini { width: 48px; flex: none; border-radius: 8px; overflow: hidden; border: 1px solid var(--border); }
h2 { margin: 0; font-size: 1.15rem; }
.buyer { font-size: .9rem; color: #bcd3f7; }
.when { display: grid; grid-template-columns: repeat(3, auto); justify-content: space-between; gap: 8px; }
.when div { display: grid; line-height: 1.5; }
.when b { font-size: .95rem; font-weight: 500; }
.seats { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; border-radius: 6px; padding: 0 10px; font-weight: 700; }
.perf { position: relative; border-top: 1px dashed var(--border); margin: 2px 0; }
.perf::before, .perf::after { content: ''; position: absolute; top: -10px; width: 20px; height: 20px; border-radius: 50%; background: var(--bg); border: 1px solid var(--border); }
.perf::before { inset-inline-start: -33px; } .perf::after { inset-inline-end: -33px; }
.qrwrap { display: grid; justify-items: center; gap: 10px; }
.qr { width: 210px; background: #fff; border-radius: 12px; padding: 14px; line-height: 0; }
.qr.dim { opacity: .35; }
.qr :deep(svg) { width: 100%; height: auto; }
.code { font-family: ui-monospace, 'SF Mono', Menlo, monospace; font-size: 1.6rem; font-weight: 700; letter-spacing: .12em; }
.actions { display: grid; grid-template-columns: 1fr auto; gap: 10px; }
</style>
