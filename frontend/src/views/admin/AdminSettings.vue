<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, onMounted } from 'vue'
import Icon from '../../components/Icon.vue'
import { api, fetchObjectUrl } from '../../lib/api'
import { toast } from '../../lib/ui'
import { num } from '../../lib/format'
import * as tg from '../../lib/tg'

const pay = ref(null)
const bot = ref(null)
const webUrl = ref('')
const invite = ref('')
const saving = ref('')
const cardImg = ref('')

async function load() {
  try {
    ;[pay.value, bot.value] = await Promise.all([api.get('/api/admin/settings/payment'), api.get('/api/admin/settings/telegram')])
    webUrl.value = bot.value.webAppUrl
    cardImg.value = pay.value.configured ? await fetchObjectUrl('/api/admin/settings/card-image?t=' + Date.now()).catch(() => '') : ''
  } catch (e) { toast(e.message, 'error') }
}
onMounted(load)

const cardView = (n) => (n || '').replace(/\D/g, '').replace(/(\d{4})(?=\d)/g, '$1 ')
function onCard(e) { pay.value.cardNumber = e.target.value.replace(/\D/g, '').slice(0, 16) }

async function savePay() {
  saving.value = 'pay'
  try { await api.put('/api/admin/settings/payment', { cardNumber: pay.value.cardNumber, cardHolder: pay.value.cardHolder, cardBank: pay.value.detectedBank ? '' : pay.value.cardBank, minutes: Number(pay.value.minutes), note: pay.value.note }); toast('اطلاعات پرداخت ذخیره شد.'); await load() }
  catch (e) { toast(e.message, 'error') } finally { saving.value = '' }
}
async function saveUrl() {
  saving.value = 'url'
  try { const r = await api.put('/api/admin/settings/telegram/webapp', { url: webUrl.value }); toast(r.warning ? `ذخیره شد، ولی: ${r.warning}` : 'آدرس مینی‌اپ ذخیره و دکمهٔ منوی ربات تنظیم شد.', r.warning ? 'error' : 'success'); await load() }
  catch (e) { toast(e.message, 'error') } finally { saving.value = '' }
}
async function apply() {
  saving.value = 'apply'
  try { await api.post('/api/admin/settings/telegram/apply'); toast('نام، توضیح، فرمان‌ها و دکمهٔ منوی ربات به‌روز شد.') }
  catch (e) { toast(e.message, 'error') } finally { saving.value = '' }
}
async function makeInvite() {
  try { invite.value = (await api.post('/api/admin/settings/telegram/admin-invite')).link } catch (e) { toast(e.message, 'error') }
}
async function copy(t) { (await tg.copyText(t)) ? toast('کپی شد.') : toast('کپی نشد.', 'error') }
</script>

<template>
  <div class="page-head"><div><h1>پرداخت و ربات</h1><p class="muted">اطلاعات کارت مقصد، مهلت پرداخت و تنظیمات ربات تلگرام و مینی‌اپ.</p></div></div>
  <Skeleton v-if="!pay || !bot" variant="form" />

  <div v-else class="stack wrap">
    <!-- پرداخت -->
    <form class="panel sec" @submit.prevent="savePay">
      <h3><Icon name="card" :size="18" /> پرداخت کارت‌به‌کارت</h3>
      <div v-if="!pay.configured" class="alert error small">تا شمارهٔ کارت را ثبت نکنید، کاربران نمی‌توانند رزرو کنند.</div>
      <label class="field">شمارهٔ کارت مقصد (۱۶ رقم)
        <input :value="cardView(pay.cardNumber)" dir="ltr" inputmode="numeric" placeholder="6037 0000 0000 0000" autocomplete="off" @input="onCard" />
      </label>
      <div class="two">
        <label class="field">نام صاحب کارت <input v-model="pay.cardHolder" /></label>
        <div class="field">بانک (تشخیص خودکار)
          <div class="detected"><img v-if="pay.bankLogo" :src="pay.bankLogo" alt="" width="28" height="28" /><span>{{ pay.detectedBank || (pay.cardNumber.length >= 6 ? 'بانک ناشناس' : 'بعد از ذخیرهٔ شمارهٔ کارت مشخص می‌شود') }}</span></div>
        </div>
      </div>
      <label class="field">مهلت پرداخت (دقیقه) <input v-model.number="pay.minutes" type="number" min="5" max="240" required /><span class="field-hint">بعد از این مدت، رزرو پرداخت‌نشده منقضی و صندلی‌ها آزاد می‌شود.</span></label>
      <label class="field">توضیح برای کاربر (اختیاری) <textarea v-model="pay.note" rows="2" placeholder="مثلاً: لطفاً توضیحات واریز را خالی بگذارید" /></label>
      <figure v-if="cardImg" class="cardprev"><img :src="cardImg" alt="" /><figcaption class="muted small">همین تصویر همراه راهنمای پرداخت برای کاربر ارسال می‌شود؛ بانک از روی شمارهٔ کارت تشخیص داده می‌شود.</figcaption></figure>
      <div><button class="btn" :disabled="saving === 'pay'">ذخیره</button></div>
    </form>

    <!-- ربات -->
    <section class="panel sec">
      <h3><Icon name="send" :size="18" /> ربات تلگرام</h3>
      <div v-if="!bot.configured" class="alert error small">توکن ربات تنظیم نشده است (Telegram:BotToken).</div>
      <div v-else class="stats">
        <div><span class="muted small">ربات</span><b dir="ltr"><a :href="bot.botLink" target="_blank" rel="noopener">@{{ bot.botUsername }}</a></b></div>
        <div><span class="muted small">کاربران تلگرام</span><b>{{ num(bot.usersWithTelegram) }}</b></div>
        <div><span class="muted small">قابل‌ارسال</span><b>{{ num(bot.reachable) }}</b></div>
        <div><span class="muted small">در صف ارسال</span><b>{{ num(bot.queued) }}</b></div>
      </div>

      <label class="field">آدرس مینی‌اپ (HTTPS)
        <input v-model="webUrl" dir="ltr" placeholder="https://xxxx.ngrok-free.app" autocomplete="off" />
        <span class="field-hint">تلگرام فقط آدرس HTTPS را برای مینی‌اپ می‌پذیرد. برای تست می‌توانید با <code dir="ltr">ngrok http 5173</code> یک آدرس موقت بگیرید.</span>
      </label>
      <div v-if="bot.webAppUrl && !bot.webAppHttps" class="alert error small">این آدرس HTTPS نیست؛ دکمهٔ مینی‌اپ ساخته نمی‌شود.</div>
      <div class="row">
        <button class="btn" :disabled="saving === 'url'" @click="saveUrl">ذخیره و تنظیم دکمهٔ منو</button>
        <button class="btn secondary" :disabled="saving === 'apply'" @click="apply">به‌روزرسانی نام، توضیح و فرمان‌های ربات</button>
      </div>

      <h4>ادمین‌ها <span class="muted small">({{ num(bot.adminCount) }} ادمین تلگرام)</span></h4>
      <p class="muted small" style="margin: 0">برای افزودن ادمین، لینک یک‌بارمصرف بسازید و برای همان نفر بفرستید؛ با بازکردنش در تلگرام ادمین می‌شود.</p>
      <div class="row">
        <button class="btn secondary" @click="makeInvite">ساخت لینک دعوت ادمین</button>
        <template v-if="invite"><input :value="invite" readonly dir="ltr" class="grow" /><button class="btn sm" @click="copy(invite)"><Icon name="copy" :size="15" /> کپی</button></template>
      </div>
    </section>
  </div>
</template>

<style scoped>
.wrap { max-width: 720px; }
.sec { padding: 20px; display: grid; gap: 14px; }
.sec h3 { margin: 0; display: flex; align-items: center; gap: 8px; }
.sec h4 { margin: 8px 0 0; }
.two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 10px; }
.stats > div { display: grid; padding: 10px 12px; border-radius: 10px; background: var(--surface-2); }
.stats b { font-size: 1.05rem; }
code { background: var(--surface-2); padding: 0 6px; border-radius: 4px; }
@media (max-width: 560px) { .two { grid-template-columns: 1fr; } }
.cardprev { margin: 0; display: grid; gap: 8px; justify-items: start; }
.cardprev img { width: min(100%, 380px); border-radius: 16px; box-shadow: 0 10px 30px rgb(0 0 0 / 45%); }
.detected { display: flex; align-items: center; gap: 8px; min-height: 42px; padding: 6px 12px; border-radius: 10px; background: var(--surface-2); border: 1px solid var(--border); color: var(--muted); }
.detected img { border-radius: 7px; background: #fff; padding: 1px; }
</style>
