<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import Icon from '../../components/Icon.vue'
import Skeleton from '../../components/Skeleton.vue'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { num, fmtDate, fmtTime } from '../../lib/format'

const st = ref(null)
let timer
const mb = (b) => num(Math.max(0.1, Math.round((b / 1048576) * 10) / 10))

async function load() { try { st.value = await api.get('/api/admin/backup') } catch (e) { toast(e.message, 'error') } }
onMounted(() => { load(); timer = setInterval(() => (st.value?.running || document.hidden ? load() : null), 3000) })
onBeforeUnmount(() => clearInterval(timer))

const total = computed(() => (st.value ? st.value.dbBytes + st.value.filesBytes : 0))
const parts = computed(() => (st.value ? Math.max(1, Math.ceil(total.value / (st.value.partMb * 1048576))) : 1))

async function send(scope) {
  if (scope === 'all') {
    const ok = await confirmDialog({ title: 'ارسال برای همهٔ ادمین‌ها', message: 'فایل پشتیبان برای همهٔ ادمین‌ها در تلگرام فرستاده می‌شود. ادامه می‌دهی؟', confirmText: 'ارسال' })
    if (!ok) return
  }
  try {
    await api.post('/api/admin/backup/send', { scope })
    toast(scope === 'all' ? 'پشتیبان‌گیری شروع شد؛ برای همهٔ ادمین‌ها ارسال می‌شود.' : 'پشتیبان‌گیری شروع شد؛ چند لحظه دیگه توی تلگرام برات میاد.')
    await load()
  } catch (e) { toast(e.message, 'error') }
}

async function toggleAuto() {
  const v = !st.value.auto
  try { await api.put('/api/admin/backup/auto', { enabled: v }); st.value.auto = v; toast(v ? 'ارسال خودکار شبانه روشن شد.' : 'ارسال خودکار شبانه خاموش شد.') }
  catch (e) { toast(e.message, 'error') }
}
</script>

<template>
  <div class="page-head">
    <div><h1>پشتیبان‌گیری</h1><p class="muted">نسخهٔ کامل دیتابیس، رسیدها و عکس فیلم‌ها در تلگرام ادمین‌ها.</p></div>
  </div>

  <Skeleton v-if="!st" variant="panels" />
  <template v-else>
    <section class="panel box">
      <div class="head">
        <div>
          <h3>ارسال خودکار شبانه</h3>
          <p class="muted small">هر شب ساعت ۱۲ (نیمه‌شب) برای همهٔ ادمین‌ها در تلگرام فرستاده می‌شود.</p>
        </div>
        <label class="switch">
          <input type="checkbox" :checked="st.auto" @change="toggleAuto" />
          <span class="knob" /><span class="lbl">{{ st.auto ? 'روشن' : 'خاموش' }}</span>
        </label>
      </div>
      <p v-if="st.auto" class="muted small next"><Icon name="clock" :size="14" /> بعدی: {{ fmtDate(st.nextAt) }} ساعت {{ fmtTime(st.nextAt) }}</p>
    </section>

    <section class="panel box">
      <h3>دریافت دستی</h3>
      <div class="info">
        <div><span class="muted small">دیتابیس</span><b>{{ mb(st.dbBytes) }} مگابایت</b></div>
        <div><span class="muted small">رسیدها و عکس‌ها</span><b>{{ mb(st.filesBytes) }} مگابایت</b><span class="muted small">{{ num(st.files) }} فایل</span></div>
        <div><span class="muted small">تعداد بخش‌ها</span><b>{{ num(parts) }}</b></div>
      </div>
      <p v-if="parts > 1" class="note small">
        تلگرام فایل بالای ۵۰ مگابایت را از ربات قبول نمی‌کند، پس فایل به {{ num(parts) }} بخش {{ num(st.partMb) }} مگابایتی تقسیم می‌شود.
        بعد از دریافت، همه را کنار هم بگذار و یکی کن: <code dir="ltr">cat name.zip.* &gt; name.zip</code>
      </p>
      <div class="acts">
        <button class="btn" :disabled="st.running" @click="send('me')"><Icon name="download" :size="17" /> ارسال برای خودم</button>
        <button class="btn secondary" :disabled="st.running" @click="send('all')">ارسال برای همهٔ ادمین‌ها</button>
      </div>
      <p class="muted small">در ربات هم می‌توانی دستور <code>/backup</code> را بزنی.</p>
      <div v-if="st.running" class="alert info run"><span class="spin" /> {{ st.progress || 'در حال انجام…' }}</div>
    </section>

    <section class="panel box">
      <h3>آخرین پشتیبان</h3>
      <p v-if="!st.last" class="muted small">هنوز پشتیبانی گرفته نشده است.</p>
      <div v-else class="last" :class="{ bad: !st.last.ok }">
        <div class="row1"><b>{{ fmtDate(st.last.at) }} · {{ fmtTime(st.last.at) }}</b><span class="badge" :class="st.last.ok ? 'ok' : 'warn'">{{ st.last.ok ? 'موفق' : 'ناموفق' }}</span></div>
        <div class="muted small">
          {{ st.last.trigger === 'auto' ? 'خودکار' : 'دستی' }} · {{ mb(st.last.bytes) }} مگابایت · {{ num(st.last.parts) }} بخش · {{ num(st.last.recipients) }} گیرنده
        </div>
        <div v-if="!st.last.ok" class="small warn">{{ st.last.message }}</div>
      </div>
    </section>
  </template>
</template>

<style scoped>
.box { padding: 16px; margin-bottom: 14px; display: grid; gap: 12px; }
.box h3 { margin: 0; }
.box p { margin: 0; }
.head { display: flex; align-items: flex-start; justify-content: space-between; gap: 14px; }
.next { display: flex; align-items: center; gap: 6px; }
.info { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
.info > div { display: grid; gap: 2px; padding: 10px 12px; border-radius: 10px; background: var(--surface-2); }
.info b { font-size: 1.05rem; }
.note { padding: 10px 12px; border-radius: 10px; background: var(--surface-2); border: 1px dashed var(--border); line-height: 1.9; }
code { background: var(--surface); padding: 0 6px; border-radius: 4px; }
.acts { display: flex; gap: 10px; flex-wrap: wrap; }
.run { display: flex; align-items: center; gap: 10px; }
.spin { width: 14px; height: 14px; border-radius: 50%; border: 2px solid currentColor; border-top-color: transparent; animation: sp .8s linear infinite; flex: none; }
@keyframes sp { to { transform: rotate(360deg); } }
.last { display: grid; gap: 4px; }
.row1 { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.warn { color: var(--warn); }
.switch { display: inline-flex; align-items: center; gap: 8px; cursor: pointer; font-size: .8rem; color: var(--muted); position: relative; flex: none; }
.switch input { position: absolute; width: 1px; height: 1px; margin: 0; padding: 0; opacity: 0; pointer-events: none; }
.knob { position: relative; width: 34px; height: 20px; border-radius: 999px; background: var(--surface-2); border: 1px solid var(--border); transition: background .15s; flex: none; }
.knob::after { content: ''; position: absolute; top: 2px; inset-inline-start: 2px; width: 14px; height: 14px; border-radius: 50%; background: var(--muted); transition: all .15s; }
.switch input:checked + .knob { background: var(--primary); border-color: var(--primary); }
.switch input:checked + .knob::after { inset-inline-start: 16px; background: #fff; }
.lbl { min-width: 32px; }
@media (max-width: 640px) {
  .info { grid-template-columns: 1fr 1fr; }
  .acts .btn { flex: 1 1 100%; justify-content: center; }
}
</style>
