<script setup>
import { seatLines } from '../../lib/seat'
import { ref, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import QrScanner from 'qr-scanner'
import Icon from '../../components/Icon.vue'
import { api } from '../../lib/api'
import { beepOk, beepFail, unlockAudio, soundEnabled, setSoundEnabled } from '../../lib/beep'
import { money, num, fmtDate, fmtTime, fmtFull, fmtCode } from '../../lib/format'

const route = useRoute()
const code = ref('')
const result = ref(null)
const error = ref('')
const busy = ref(false)
const entered = ref(false) // ورود همین الان ثبت شد
const sound = ref(soundEnabled())
const lastSource = ref('manual') // camera | image | manual

const STATUS = {
  valid: { tone: 'ok', title: 'بلیت معتبر است', text: 'مشخصات را کنترل کنید و ورود را ثبت کنید.' },
  used: { tone: 'bad', title: 'قبلاً استفاده شده', text: 'این بلیت پیش‌تر برای ورود ثبت شده است.' },
  expired: { tone: 'warn', title: 'سانس برگزار شده', text: 'زمان این سانس گذشته است.' },
  early: { tone: 'info', title: 'هنوز زود است', text: 'ورود تا ۲ ساعت پیش از شروع سانس آغاز می‌شود.' },
}

function toggleSound() {
  sound.value = !sound.value
  setSoundEnabled(sound.value)
  if (sound.value) { unlockAudio(); beepOk() }
}

async function check(c = code.value) {
  const clean = c.replace(/[^a-zA-Z0-9]/g, '')
  if (!clean) return
  unlockAudio()
  busy.value = true; error.value = ''; result.value = null; entered.value = false
  try {
    result.value = await api.get(`/api/admin/tickets/${clean}`)
    result.value.status === 'valid' ? beepOk() : beepFail()
  } catch (e) {
    error.value = e.message
    beepFail()
  } finally { busy.value = false }
}

async function checkIn() {
  busy.value = true; error.value = ''
  try {
    result.value = await api.post(`/api/admin/tickets/${result.value.code}/checkin`)
    entered.value = true
    beepOk()
  } catch (e) {
    error.value = e.message
    beepFail()
    await refresh()
  } finally { busy.value = false }
}
async function refresh() {
  try { result.value = await api.get(`/api/admin/tickets/${result.value.code}`) } catch { /* ignore */ }
}

// ---------- اسکن ----------
const scanning = ref(false)
const video = ref(null)
let scanner = null

function onDecoded(text, source) {
  stopScan()
  lastSource.value = source
  code.value = text
  check(text)
}
async function startScan() {
  unlockAudio()
  error.value = ''
  if (!(await QrScanner.hasCamera())) { error.value = 'دوربینی روی این دستگاه پیدا نشد؛ عکس QR را انتخاب کنید یا کد را دستی وارد کنید.'; beepFail(); return }
  result.value = null
  scanning.value = true
  await nextTick()
  scanner = new QrScanner(video.value, (r) => onDecoded(r.data, 'camera'), {
    preferredCamera: 'environment', returnDetailedScanResult: true, highlightScanRegion: true, highlightCodeOutline: true,
  })
  try { await scanner.start() }
  catch { error.value = 'دسترسی به دوربین ممکن نشد. اجازهٔ دوربین را به مرورگر بدهید (فقط روی HTTPS یا localhost کار می‌کند).'; beepFail(); stopScan() }
}
function stopScan() {
  scanner?.stop(); scanner?.destroy(); scanner = null; scanning.value = false
}
async function fromImage(e) {
  const file = e.target.files?.[0]
  e.target.value = ''
  if (!file) return
  unlockAudio()
  error.value = ''
  try { onDecoded((await QrScanner.scanImage(file, { returnDetailedScanResult: true })).data, 'image') }
  catch { error.value = 'QR در این تصویر پیدا نشد.'; beepFail() }
}
onMounted(() => { // لینک از صفحهٔ «بلیت‌های فروخته‌شده»
  const c = route.query.code
  if (typeof c === 'string' && c) { code.value = c; check(c) }
})
onBeforeUnmount(stopScan)
</script>

<template>
  <div class="page-head">
    <div><h1>بررسی بلیت</h1><p class="muted">در ورودی سینما، QR بلیت را اسکن کنید و ورود را ثبت کنید.</p></div>
    <button class="btn secondary sm" :aria-pressed="sound" @click="toggleSound">
      <Icon :name="sound ? 'volume' : 'mute'" :size="16" /> صدا {{ sound ? 'روشن' : 'خاموش' }}
    </button>
  </div>

  <div class="grid">
    <section class="panel scan">
      <h3>۱. بلیت را وارد کنید</h3>

      <button class="btn lg big" @click="scanning ? stopScan() : startScan()">
        <Icon :name="scanning ? 'x' : 'qr'" :size="20" /> {{ scanning ? 'توقف اسکن' : 'اسکن با دوربین' }}
      </button>
      <div v-show="scanning" class="cam"><video ref="video" muted playsinline /></div>

      <div class="or"><span>یا</span></div>

      <label class="btn secondary file">
        <Icon name="upload" :size="17" /> انتخاب عکس QR
        <input type="file" accept="image/*" hidden @change="fromImage" />
      </label>

      <div class="or"><span>یا</span></div>

      <form class="row" @submit.prevent="check()">
        <input v-model="code" dir="ltr" placeholder="کد بلیت: XXXX-XXXX" maxlength="12" autocomplete="off" aria-label="کد بلیت" class="grow" />
        <button class="btn secondary" :disabled="busy || !code.trim()">بررسی</button>
      </form>
    </section>

    <section class="panel result" aria-live="polite">
      <h3>۲. نتیجه</h3>

      <div v-if="error" class="banner bad"><Icon name="alert" :size="22" /><div><b>{{ error }}</b></div></div>

      <div v-if="!result && !error" class="placeholder">
        <Icon name="ticket" :size="34" />
        <p class="muted">پس از اسکن یا وارد کردن کد، مشخصات بلیت اینجا نشان داده می‌شود.</p>
      </div>

      <template v-if="result">
        <div class="banner" :class="entered ? 'ok' : STATUS[result.status].tone" role="status">
          <Icon :name="entered || result.status === 'valid' ? 'check' : 'alert'" :size="24" />
          <div>
            <b>{{ entered ? 'ورود ثبت شد' : STATUS[result.status].title }}</b>
            <span v-if="!entered">{{ STATUS[result.status].text }}<template v-if="result.checkedInAt"> ({{ fmtFull(result.checkedInAt) }})</template></span>
          </div>
        </div>

        <h3 class="movie">{{ result.movie }}</h3>
        <p class="muted" style="margin: 0">{{ result.cinema }} · {{ result.hall }}</p>
        <p style="margin: 4px 0 12px">{{ fmtDate(result.startsAt) }} · <b>{{ fmtTime(result.startsAt) }}</b></p>
        <div class="seats"><span v-for="l in seatLines(result.seats)" :key="l" class="chip">{{ l }}</span></div>
        <div class="muted small">خریدار: {{ result.buyer }} · کد {{ fmtCode(result.code) }}</div>
        <div class="muted small">{{ num(result.seats.length) }} صندلی · {{ money(result.totalPrice) }}</div>

        <button v-if="result.status === 'valid' && !entered" class="btn lg big" :disabled="busy" @click="checkIn">ثبت ورود</button>
        <button v-else-if="lastSource === 'camera'" class="btn secondary big" @click="startScan"><Icon name="qr" :size="18" /> اسکن بعدی</button>
      </template>
    </section>
  </div>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: minmax(0, 380px) minmax(0, 1fr); gap: 18px; align-items: start; }
.panel { padding: 20px; display: grid; gap: 14px; }
.panel h3 { margin: 0; }
.big { width: 100%; }
.file { cursor: pointer; width: 100%; }
.cam { border-radius: 12px; overflow: hidden; border: 1px solid var(--border); background: #000; }
.cam video { width: 100%; display: block; aspect-ratio: 1; object-fit: cover; }
.or { display: flex; align-items: center; gap: 10px; color: var(--muted); font-size: .8rem; }
.or::before, .or::after { content: ''; flex: 1; height: 1px; background: var(--border); }
input.grow { letter-spacing: .08em; }
.placeholder { display: grid; justify-items: center; gap: 6px; padding: 36px 12px; color: var(--muted); text-align: center; }
.placeholder p { margin: 0; max-width: 32ch; }
.movie { margin: 4px 0 0 !important; font-size: 1.2rem; }
.seats { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; border-radius: 6px; padding: 0 10px; font-weight: 700; }
.banner { display: flex; gap: 12px; align-items: center; padding: 14px 16px; border-radius: 10px; border: 1px solid; }
.banner div { display: grid; line-height: 1.5; }
.banner b { font-size: 1.05rem; }
.banner span { font-size: .88rem; opacity: .85; }
.banner.ok { background: #0f2619; color: #86efac; border-color: #1d4a2e; }
.banner.bad { background: #2a1417; color: #fca5a5; border-color: #4a2428; }
.banner.warn { background: #33270e; color: #f0b952; border-color: #5a4514; }
.banner.info { background: var(--primary-soft); color: #9cc2ff; border-color: #1f3a68; }
@media (max-width: 860px) { .grid { grid-template-columns: minmax(0, 1fr); } }
</style>
