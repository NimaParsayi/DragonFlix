<script setup>
import { runBusy } from '../lib/busy'
import Skeleton from '../components/Skeleton.vue'
import { seatLines } from '../lib/seat'
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { useRouter } from 'vue-router'
import Icon from '../components/Icon.vue'
import PosterArt from '../components/PosterArt.vue'
import TicketModal from '../components/TicketModal.vue'
import { api, downloadFile } from '../lib/api'
import { toast, confirmDialog } from '../lib/ui'
import { money, num, fmtDate, fmtTime, fmtCode } from '../lib/format'
import * as tg from '../lib/tg'

const props = defineProps({ id: String })
const router = useRouter()

const b = ref(null) // رزرو
const pay = ref(null) // اطلاعات پرداخت
const error = ref('')
const busy = ref(false)
const showTicket = ref(false)
const deadlineAt = ref(0) // بر حسب ms ساعت همین دستگاه
const now = ref(Date.now())
const form = ref({ file: null, preview: '' })
const logoFail = ref(false)
const step = ref('pay') // pay = شمارهٔ کارت، receipt = عکس رسید
function goStep(x) { step.value = x; tg.haptic.tap(); window.scrollTo({ top: 0, behavior: 'smooth' }) }
let tick, poll

// ---- بارگذاری و به‌روزرسانی (هر ۵ ثانیه تا وضعیت نهایی شود) ----
async function load(first = false) {
  try {
    const [booking, payment] = await Promise.all([api.get(`/api/bookings/${props.id}`), api.get(`/api/bookings/${props.id}/payment`)])
    const was = b.value?.status
    b.value = booking
    pay.value = payment
    // اختلاف ساعت سرور و دستگاه: فقط «فاصلهٔ مهلت تا الان سرور» مهم است، نه ساعت مطلق
    if (payment.payDeadline) deadlineAt.value = Date.now() + (new Date(payment.payDeadline) - new Date(payment.serverNow))
    if (!first && was && was !== booking.status) { booking.status === 'Confirmed' ? tg.haptic.success() : tg.haptic.warn() }
  } catch (e) { if (first) error.value = e.message }
}
onMounted(() => {
  load(true)
  tick = setInterval(() => (now.value = Date.now()), 1000)
  poll = setInterval(() => { if (!document.hidden && ['PendingPayment', 'AwaitingReview'].includes(b.value?.status)) load() }, 5000)
})
onBeforeUnmount(() => { clearInterval(tick); clearInterval(poll); tg.mainButton.hide() })

const status = computed(() => b.value?.status)
const remaining = computed(() => (status.value === 'PendingPayment' ? Math.max(0, Math.floor((deadlineAt.value - now.value) / 1000)) : null))
const mmss = computed(() => { const s = remaining.value ?? 0; return `${num(Math.floor(s / 60))}:${num(Math.floor((s % 60) / 10))}${num(s % 10)}` })
const seats = computed(() => (b.value?.seats?.length ? b.value.seats.map((s) => `${s.row}${s.number}`) : (b.value?.seatsText || '').split(',').filter(Boolean)))
const card = computed(() => (pay.value?.card?.number || '').replace(/(\d{4})(?=\d)/g, '$1 '))

// وقتی مهلت تمام شد، وضعیت را از سرور بگیر
watch(remaining, (r) => { if (r === 0) load() })

// ---- کپی ----
async function copy(text, label) {
  if (await tg.copyText(text)) { tg.haptic.tap(); toast(`${label} کپی شد.`) } else toast('کپی نشد؛ دستی انتخابش کن.', 'error')
}

// ---- عکس رسید: پیش از ارسال کوچک می‌شود (عکس‌های گوشی چندمگابایتی‌اند) ----
async function pickFile(e) {
  const f = e.target.files?.[0]
  e.target.value = ''
  if (!f) return
  if (!f.type.startsWith('image/')) return toast('فقط تصویر انتخاب کن.', 'error')
  try {
    const out = await shrink(f)
    form.value.file = out
    if (form.value.preview) URL.revokeObjectURL(form.value.preview)
    form.value.preview = URL.createObjectURL(out)
  } catch { toast('خواندن عکس ممکن نشد.', 'error') }
}
async function shrink(file, max = 1600) {
  const bmp = await createImageBitmap(file)
  const k = Math.min(1, max / Math.max(bmp.width, bmp.height))
  const c = Object.assign(document.createElement('canvas'), { width: Math.round(bmp.width * k), height: Math.round(bmp.height * k) })
  c.getContext('2d').drawImage(bmp, 0, 0, c.width, c.height)
  return new Promise((res, rej) => c.toBlob((x) => (x ? res(new File([x], 'receipt.jpg', { type: 'image/jpeg' })) : rej()), 'image/jpeg', 0.85))
}
const removeFile = () => { if (form.value.preview) URL.revokeObjectURL(form.value.preview); form.value.file = null; form.value.preview = '' }

const canSubmit = computed(() => !busy.value && remaining.value !== 0 && !!form.value.file)

async function submit() {
  if (!canSubmit.value) { toast('عکس رسید رو اضافه کن.', 'error'); tg.haptic.warn(); return }
  busy.value = true
  await runBusy('در حال ارسال رسید…', async (step) => {
    try {
      const fd = new FormData()
      fd.append('file', form.value.file)
      await api.upload(`/api/bookings/${props.id}/receipt`, fd)
      tg.haptic.success()
      step('رسید ارسال شد ✓', 'در حال به‌روزرسانی وضعیت…')
      await load()
    } catch (e) { toast(e.message, 'error'); tg.haptic.error(); await load() }
  }, 'عکس رسید ممکن است چند ثانیه طول بکشد')
  busy.value = false
}

async function cancel() {
  const ok = await confirmDialog({ title: 'انصراف از رزرو', message: 'صندلی‌ها آزاد می‌شود و این رزرو لغو خواهد شد.', confirmText: 'لغو رزرو', danger: true })
  if (!ok) return
  await runBusy('در حال لغو رزرو…', async () => {
    try { await api.post(`/api/bookings/${props.id}/cancel`); await load() } catch (e) { toast(e.message, 'error') }
  })
}

async function pdf() {
  try { await downloadFile(`/api/bookings/${props.id}/pdf`, `ticket-${b.value.code}.pdf`) } catch (e) { toast(e.message, 'error') }
}

// دکمهٔ اصلی بومی تلگرام: مرحلهٔ ۱ «واریز کردم»، مرحلهٔ ۲ «ارسال رسید»
watch([status, step, canSubmit, busy, remaining], () => {
  if (!tg.mainButton.supported) return
  if (status.value !== 'PendingPayment') return tg.mainButton.hide()
  if (step.value === 'pay') tg.mainButton.show({ text: 'واریز کردم', onClick: () => goStep('receipt'), disabled: remaining.value === 0 })
  else tg.mainButton.show({ text: 'ارسال رسید', onClick: submit, loading: busy.value, disabled: !canSubmit.value })
}, { immediate: true })
</script>

<template>
  <div v-if="error" class="alert error">{{ error }}</div>
  <Skeleton v-else-if="!b || !pay" variant="pay" />

  <div v-else class="pay stack">
    <!-- خلاصهٔ رزرو -->
    <section class="card sum">
      <div class="top">
        <div class="thumb"><PosterArt :movie="{ title: b.movie, genre: b.genre, posterUrl: b.posterUrl }" :show-title="false" /></div>
        <div class="tinfo">
          <span class="st" :class="status">{{ { PendingPayment: 'منتظر پرداخت', AwaitingReview: 'در انتظار تأیید', Confirmed: 'تأییدشده', Rejected: 'ردشده', Expired: 'منقضی', Cancelled: 'لغوشده' }[status] }}</span>
          <h2>{{ b.movie }}</h2>
          <div class="muted">{{ b.cinema }} · {{ b.hall }}</div>
          <div class="when"><Icon name="calendar" :size="16" /> {{ fmtDate(b.startsAt) }} <b>{{ fmtTime(b.startsAt) }}</b></div>
        </div>
      </div>
      <div class="seats"><span v-for="l in seatLines(seats)" :key="l" class="chip">{{ l }}</span></div>
      <div class="amount"><span class="muted">مبلغ کل</span><b>{{ money(b.totalPrice) }}</b></div>
    </section>

    <!-- ۱) منتظر پرداخت -->
    <template v-if="status === 'PendingPayment'">
      <div class="timer" :class="{ low: remaining < 120 }" role="timer">
        <span><Icon name="clock" :size="16" /> مهلت پرداخت</span><b>{{ mmss }}</b>
      </div>

      <!-- مرحلهٔ ۱: واریز -->
      <template v-if="step === 'pay'">
        <section class="card">
          <h3><span class="n">۱</span> مبلغ رو کارت‌به‌کارت کن</h3>
          <div class="cardbox">
            <div class="bank">
              <img v-if="pay.card.bankLogo && !logoFail" :src="pay.card.bankLogo" alt="" width="44" height="44" @error="logoFail = true" />
              <div class="bi"><b v-if="pay.card.bank">{{ pay.card.bank }}</b><span v-if="pay.card.holder" class="muted small">به نام {{ pay.card.holder }}</span></div>
            </div>
            <div class="num" dir="ltr">{{ card }}</div>
            <div class="copyrow">
              <button class="btn secondary sm" @click="copy(pay.card.number, 'شمارهٔ کارت')"><Icon name="copy" :size="15" /> کپی شمارهٔ کارت</button>
              <button class="btn secondary sm" @click="copy(String(Math.round(b.totalPrice)), 'مبلغ')"><Icon name="copy" :size="15" /> کپی مبلغ</button>
            </div>
          </div>
          <p class="note">دقیقاً <b>{{ money(b.totalPrice) }}</b> واریز کن.</p>
          <p v-if="pay.note" class="note extra">{{ pay.note }}</p>
          <button v-if="!tg.mainButton.supported" class="btn lg" style="width: 100%; margin-top: 14px" :disabled="remaining === 0" @click="goStep('receipt')">واریز کردم</button>
        </section>
      </template>

      <!-- مرحلهٔ ۲: عکس رسید -->
      <section v-else class="card">
        <h3><span class="n">۲</span> عکس رسید رو بفرست</h3>
        <p class="note" style="margin-top: 0">از رسید بانک اسکرین‌شات بگیر و همین‌جا اضافه‌اش کن؛ مدیر بعد از بررسی بلیتت رو صادر می‌کنه.</p>

        <div class="photo">
          <div v-if="form.preview" class="pv"><img :src="form.preview" alt="رسید" /><button class="btn danger sm" @click="removeFile"><Icon name="trash" :size="14" /> حذف</button></div>
          <label v-else class="drop">
            <Icon name="camera" :size="30" />
            <b>افزودن عکس رسید</b>
            <span class="muted small">از گالری انتخاب کن یا عکس بگیر</span>
            <input type="file" accept="image/*" hidden @change="pickFile" />
          </label>
        </div>

        <button v-if="!tg.mainButton.supported" class="btn lg" style="width: 100%" :disabled="!canSubmit" @click="submit">{{ busy ? 'در حال ارسال…' : 'ارسال رسید' }}</button>
        <button class="link back" @click="goStep('pay')">← بازگشت به شمارهٔ کارت</button>
      </section>

      <button class="link danger" @click="cancel">انصراف از رزرو</button>
    </template>

    <!-- ۲) در انتظار تأیید -->
    <section v-else-if="status === 'AwaitingReview'" class="card state wait">
      <div class="big"><Icon name="clock" :size="34" /></div>
      <h3>رسیدت ثبت شد</h3>
      <p>مدیر داره پرداختت رو بررسی می‌کنه. نتیجه همین‌جا و توی تلگرام بهت خبر داده می‌شه؛ صندلی‌هات تا اون موقع برات نگه داشته شده.</p>
      <div v-if="b.trackingCode" class="muted small">شمارهٔ پیگیری: <b dir="ltr">{{ b.trackingCode }}</b></div>
      <RouterLink class="btn secondary" :to="{ name: 'my-bookings' }">رزروهای من</RouterLink>
    </section>

    <!-- ۳) تأیید شد -->
    <section v-else-if="status === 'Confirmed'" class="card state ok">
      <div class="big"><Icon name="check" :size="34" /></div>
      <h3>پرداخت تأیید شد؛ بلیتت صادر شد</h3>
      <div class="code" dir="ltr">{{ fmtCode(b.code) }}</div>
      <p class="muted small">نسخهٔ بلیت در تلگرام هم برایتان ارسال شد.</p>
      <div class="acts">
        <button class="btn" @click="showTicket = true"><Icon name="qr" :size="17" /> نمایش بلیت</button>
        <button class="btn secondary" @click="pdf"><Icon name="download" :size="17" /> PDF</button>
      </div>
    </section>

    <!-- ۴) ردشده -->
    <section v-else-if="status === 'Rejected'" class="card state bad">
      <div class="big"><Icon name="alert" :size="34" /></div>
      <h3>پرداخت تأیید نشد</h3>
      <p v-if="b.rejectReason">دلیل: {{ b.rejectReason }}</p>
      <p class="muted small">صندلی‌ها آزاد شدن. اگه واریز کردی با پشتیبانی تماس بگیر یا دوباره رزرو کن.</p>
      <RouterLink class="btn" :to="{ name: 'movie', params: { id: b.movieId } }">رزرو دوباره</RouterLink>
    </section>

    <!-- ۵) منقضی / لغو -->
    <section v-else class="card state">
      <div class="big"><Icon name="clock" :size="34" /></div>
      <h3>{{ status === 'Expired' ? 'مهلت پرداخت تمام شد' : 'رزرو لغو شد' }}</h3>
      <p class="muted">صندلی‌ها آزاد شدن. می‌تونی دوباره رزرو کنی.</p>
      <RouterLink class="btn" :to="{ name: 'movie', params: { id: b.movieId } }">رزرو دوباره</RouterLink>
    </section>
  </div>

  <TicketModal v-if="showTicket && b" :booking="b" @close="showTicket = false" />
</template>

<style scoped>
.pay { max-width: 560px; margin-inline: auto; }
.sum { display: grid; gap: 10px; }
.top { display: flex; gap: 14px; align-items: flex-start; }
.thumb { width: 92px; flex: none; border-radius: 10px; overflow: hidden; border: 1px solid var(--border); box-shadow: 0 8px 22px rgb(0 0 0 / 40%); }
.tinfo { display: grid; gap: 5px; justify-items: start; min-width: 0; flex: 1; }
.sum h2 { margin: 0; font-size: 1.2rem; line-height: 1.5; }
.when { display: flex; align-items: center; gap: 8px; }
.when b { color: var(--sky); }
.seats { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; border-radius: 6px; padding: 0 10px; font-weight: 700; }
.amount { display: flex; justify-content: space-between; align-items: baseline; padding-top: 10px; margin-top: 4px; border-top: 1px dashed var(--border); }
.amount b { font-size: 1.3rem; }
.st { padding: 1px 12px; border-radius: 999px; font-size: .8rem; background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; white-space: nowrap; }
.st.PendingPayment { background: #33270e; color: #f0b952; border-color: #5a4514; }
.st.Confirmed { background: #0f2619; color: #86efac; border-color: #1d4a2e; }
.st.Rejected { background: #2a1417; color: #fca5a5; border-color: #4a2428; }
.st.Expired, .st.Cancelled { background: var(--surface-2); color: var(--muted); border-color: var(--border); }

.timer { display: flex; align-items: center; justify-content: space-between; padding: 10px 14px; border-radius: 12px; background: #261e0b; border: 1px solid #5a4514; color: #e6c67a; }
.timer span { display: inline-flex; align-items: center; gap: 6px; }
.timer b { font-size: 1.35rem; color: #f5d98a; font-variant-numeric: tabular-nums; direction: ltr; }
.timer.low { background: #2a1417; border-color: #6b2a30; color: #fca5a5; }
.timer.low b { color: #fecaca; }

h3 { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
.n { width: 26px; height: 26px; border-radius: 50%; display: grid; place-items: center; font-size: .85rem; background: var(--primary); color: #fff; }
.cardbox { display: grid; gap: 6px; padding: 14px; border-radius: 12px; background: var(--surface-2); border: 1px dashed #34405a; text-align: center; }
.num { font-size: clamp(1.05rem, 5.6vw, 1.4rem); font-weight: 700; letter-spacing: .03em; white-space: nowrap; font-family: ui-monospace, 'SF Mono', Menlo, monospace; }
.copyrow { display: flex; gap: 8px; justify-content: center; flex-wrap: wrap; margin-top: 4px; }
.note { margin: 12px 0 0; font-size: .92rem; }
.note.extra { color: var(--muted); font-size: .85rem; padding: 8px 12px; border-radius: 8px; background: var(--surface-2); }
.card :deep(.field) + :deep(.field) { margin-top: 12px; }
.photo { display: grid; gap: 8px; margin: 14px 0; justify-items: start; }
.photo { width: 100%; }
.bank { display: flex; align-items: center; justify-content: center; gap: 10px; }
.bank img { width: 44px; height: 44px; border-radius: 12px; background: #fff; padding: 3px; object-fit: contain; flex: none; }
.bank .bi { display: grid; text-align: start; line-height: 1.5; }
.drop { display: grid; justify-items: center; gap: 6px; width: 100%; padding: 28px 14px; border-radius: 14px; border: 1.5px dashed #34405a; background: var(--surface-2); color: var(--muted); cursor: pointer; text-align: center; }
.drop b { color: var(--text); }
.drop:active { background: var(--primary-soft); }
.link.back { display: block; margin: 12px auto 0; color: var(--muted); }
.pv { display: flex; gap: 12px; align-items: flex-end; }
.pv img { height: 120px; border-radius: 10px; border: 1px solid var(--border); display: block; }
.link { background: none; border: 0; font: inherit; cursor: pointer; padding: 8px; color: var(--muted); }
.link.danger:hover { color: var(--danger); }

.state { display: grid; justify-items: center; gap: 8px; text-align: center; padding: 28px 18px; }
.state h3 { margin: 0; justify-content: center; }
.state p { margin: 0; max-width: 44ch; }
.big { width: 64px; height: 64px; border-radius: 50%; display: grid; place-items: center; background: var(--primary-soft); color: var(--sky); }
.wait .big { animation: pulse 1.8s ease-in-out infinite; }
.ok .big { background: #14532d; color: #86efac; }
.bad .big { background: #2a1417; color: #fca5a5; }
.code { font-family: ui-monospace, 'SF Mono', Menlo, monospace; font-size: 1.7rem; font-weight: 700; letter-spacing: .12em; }
.acts { display: flex; gap: 10px; flex-wrap: wrap; justify-content: center; margin-top: 6px; }
@keyframes pulse { 50% { transform: scale(.94); opacity: .7; } }
</style>
