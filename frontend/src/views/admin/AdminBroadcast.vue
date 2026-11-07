<script setup>
import { ref, computed, onMounted } from 'vue'
import Icon from '../../components/Icon.vue'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { num } from '../../lib/format'

const CATS = [
  { key: 'promo', label: 'تخفیف و پیشنهاد ویژه', icon: '🎁' },
  { key: 'new_movie', label: 'اکران فیلم جدید / خبر', icon: '🎬' },
]
const aud = ref(null)
const form = ref({ category: 'promo', text: '', buttonText: '', buttonPath: '/' })
const sending = ref(false)

onMounted(async () => { try { aud.value = await api.get('/api/admin/broadcast/audience') } catch (e) { toast(e.message, 'error') } })

const count = computed(() => aud.value?.[form.value.category] ?? 0)
const icon = computed(() => CATS.find((c) => c.key === form.value.category).icon)

async function send() {
  if (form.value.text.trim().length < 3) return toast('متن اعلان را بنویسید.', 'error')
  const ok = await confirmDialog({ title: 'ارسال اعلان همگانی', message: `این پیام برای ${num(count.value)} نفر (فقط کسانی که این دسته را روشن کرده‌اند) ارسال می‌شود. ادامه می‌دهید؟`, confirmText: 'ارسال' })
  if (!ok) return
  sending.value = true
  try { const r = await api.post('/api/admin/broadcast', form.value); toast(`برای ${num(r.queued)} نفر در صف ارسال قرار گرفت.`); form.value.text = '' }
  catch (e) { toast(e.message, 'error') } finally { sending.value = false }
}
</script>

<template>
  <div class="page-head"><div><h1>اعلان همگانی</h1><p class="muted">پیام تبلیغاتی یا خبر را برای کاربرانی بفرستید که آن دسته را در «حساب من» روشن کرده‌اند. کاربری که خاموش کرده، چیزی نمی‌گیرد.</p></div></div>

  <div class="grid">
    <form class="panel f" @submit.prevent="send">
      <label class="field">دسته
        <select v-model="form.category"><option v-for="c in CATS" :key="c.key" :value="c.key">{{ c.label }}</option></select>
        <span class="field-hint">مخاطبان این دسته: <b>{{ aud ? num(count) : '…' }}</b> نفر از {{ aud ? num(aud.total) : '…' }} کاربرِ قابل‌دسترس</span>
      </label>
      <label class="field">متن پیام <textarea v-model="form.text" rows="5" maxlength="1000" placeholder="مثلاً: این آخر هفته بلیت همهٔ سانس‌های شب نیم‌بها!" required /><span class="field-hint">{{ form.text.length }}/1000</span></label>
      <div class="two">
        <label class="field">متن دکمه (اختیاری) <input v-model="form.buttonText" maxlength="30" placeholder="دیدن فیلم‌ها" /></label>
        <label class="field">مسیر باز شدن در مینی‌اپ <input v-model="form.buttonPath" dir="ltr" placeholder="/" /></label>
      </div>
      <div><button class="btn" :disabled="sending || !count"><Icon name="megaphone" :size="17" /> ارسال به {{ num(count) }} نفر</button></div>
    </form>

    <aside class="panel prev">
      <span class="muted small">پیش‌نمایش پیام در تلگرام</span>
      <div class="bubble">
        <div class="txt">{{ icon }} {{ form.text || 'متن پیام اینجا نشان داده می‌شود…' }}</div>
        <div v-if="form.buttonText" class="btnp">{{ form.buttonText }}</div>
      </div>
    </aside>
  </div>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr); gap: 18px; align-items: start; }
.f { padding: 20px; display: grid; gap: 14px; }
.two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.prev { padding: 18px; display: grid; gap: 10px; position: sticky; top: 76px; }
.bubble { background: #1b2a3d; border-radius: 14px 14px 14px 4px; padding: 10px 12px; display: grid; gap: 8px; }
.txt { white-space: pre-wrap; line-height: 1.8; }
.btnp { text-align: center; padding: 6px; border-radius: 8px; background: rgb(255 255 255 / 10%); font-size: .9rem; }
@media (max-width: 860px) { .grid { grid-template-columns: minmax(0, 1fr); } .prev { position: static; } .two { grid-template-columns: 1fr; } }
</style>
