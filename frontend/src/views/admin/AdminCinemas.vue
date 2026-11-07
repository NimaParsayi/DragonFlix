<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, onMounted } from 'vue'
import Modal from '../../components/Modal.vue'
import Icon from '../../components/Icon.vue'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { num } from '../../lib/format'

const cinemas = ref(null)
const cinemaForm = ref(null) // { id, name, city, address }
const hallForm = ref(null) // { id, cinemaId, name }
const saving = ref(false)

async function load() {
  try { cinemas.value = await api.get('/api/admin/cinemas') } catch (e) { toast(e.message, 'error') }
}
onMounted(load)

// اجرای یک عملیات: پیام موفقیت یا خطا را نشان می‌دهد و لیست را تازه می‌کند
async function run(fn, okMsg) {
  saving.value = true
  try { await fn(); toast(okMsg); await load(); return true }
  catch (e) { toast(e.message, 'error'); return false }
  finally { saving.value = false }
}

async function saveCinema() {
  const { id, ...body } = cinemaForm.value
  if (await run(() => (id ? api.put(`/api/admin/cinemas/${id}`, body) : api.post('/api/admin/cinemas', body)), id ? 'سینما ویرایش شد.' : 'سینما اضافه شد.'))
    cinemaForm.value = null
}
async function removeCinema(c) {
  const ok = await confirmDialog({ title: 'حذف سینما', message: `سینمای «${c.name}» با همهٔ سالن‌هایش حذف می‌شود. این کار قابل بازگشت نیست.`, confirmText: 'حذف', danger: true })
  if (ok) run(() => api.del(`/api/admin/cinemas/${c.id}`), 'سینما حذف شد.')
}

async function saveHall() {
  const { id, cinemaId, name } = hallForm.value
  if (await run(() => (id ? api.put(`/api/admin/halls/${id}`, { name }) : api.post(`/api/admin/cinemas/${cinemaId}/halls`, { name })), id ? 'نام سالن تغییر کرد.' : 'سالن اضافه شد؛ حالا چینش آن را بسازید.'))
    hallForm.value = null
}
async function removeHall(h) {
  const ok = await confirmDialog({ title: 'حذف سالن', message: `سالن «${h.name}» و چینش آن حذف می‌شود.`, confirmText: 'حذف', danger: true })
  if (ok) run(() => api.del(`/api/admin/halls/${h.id}`), 'سالن حذف شد.')
}
</script>

<template>
  <div class="page-head">
    <div><h1>سینماها و سالن‌ها</h1><p class="muted">هر سینما چند سالن دارد و هر سالن چینش صندلی مخصوص خودش را.</p></div>
    <button class="btn" @click="cinemaForm = { id: null, name: '', city: '', address: '' }"><Icon name="plus" :size="17" /> سینمای جدید</button>
  </div>

  <Skeleton v-if="!cinemas" variant="panels" />
  <div v-else-if="!cinemas.length" class="panel empty">
    <p>هنوز سینمایی تعریف نشده است.</p>
    <button class="btn" @click="cinemaForm = { id: null, name: '', city: '', address: '' }">اولین سینما را اضافه کنید</button>
  </div>

  <section v-for="c in cinemas" :key="c.id" class="panel cinema">
    <div class="chead">
      <div class="ico"><Icon name="building" :size="22" /></div>
      <div class="grow">
        <h3>{{ c.name }}</h3>
        <span class="muted small">{{ c.city }}<template v-if="c.address"> · {{ c.address }}</template></span>
      </div>
      <button class="btn secondary sm" @click="hallForm = { id: null, cinemaId: c.id, name: '' }"><Icon name="plus" :size="15" /> سالن</button>
      <button class="iconbtn" title="ویرایش سینما" aria-label="ویرایش سینما" @click="cinemaForm = { id: c.id, name: c.name, city: c.city, address: c.address }"><Icon name="edit" :size="17" /></button>
      <button class="iconbtn danger" title="حذف سینما" aria-label="حذف سینما" @click="removeCinema(c)"><Icon name="trash" :size="17" /></button>
    </div>

    <div v-for="h in c.halls" :key="h.id" class="list-row">
      <Icon name="seat" :size="18" class="muted" />
      <div class="grow">
        <b>{{ h.name }}</b>
        <div class="small">
          <span v-if="!h.rowCount" class="badge warn"><Icon name="alert" :size="13" /> چینش ندارد</span>
          <span v-else class="muted">{{ num(h.rowCount) }} ردیف · {{ num(h.seatCount) }} صندلی</span>
        </div>
      </div>
      <RouterLink class="btn sm" :class="{ secondary: h.rowCount }" :to="{ name: 'admin-layout', params: { id: h.id } }">
        {{ h.rowCount ? 'ویرایش چینش' : 'ساخت چینش' }}
      </RouterLink>
      <button class="iconbtn" title="تغییر نام" aria-label="تغییر نام سالن" @click="hallForm = { id: h.id, cinemaId: c.id, name: h.name }"><Icon name="edit" :size="17" /></button>
      <button class="iconbtn danger" title="حذف سالن" aria-label="حذف سالن" @click="removeHall(h)"><Icon name="trash" :size="17" /></button>
    </div>
    <div v-if="!c.halls.length" class="list-row muted small">این سینما هنوز سالنی ندارد.</div>
  </section>

  <Modal v-if="cinemaForm" :title="cinemaForm.id ? 'ویرایش سینما' : 'سینمای جدید'" @close="cinemaForm = null">
    <form id="cinema-form" class="stack" @submit.prevent="saveCinema">
      <label class="field">نام سینما <input v-model="cinemaForm.name" required autofocus /></label>
      <label class="field">شهر <input v-model="cinemaForm.city" required /></label>
      <label class="field">آدرس (اختیاری) <input v-model="cinemaForm.address" /></label>
    </form>
    <template #footer>
      <button class="btn" form="cinema-form" :disabled="saving">ذخیره</button>
      <button class="btn secondary" type="button" @click="cinemaForm = null">انصراف</button>
    </template>
  </Modal>

  <Modal v-if="hallForm" :title="hallForm.id ? 'تغییر نام سالن' : 'سالن جدید'" width="400px" @close="hallForm = null">
    <form id="hall-form" @submit.prevent="saveHall">
      <label class="field">نام سالن <input v-model="hallForm.name" required autofocus placeholder="مثلاً سالن ۳ یا سالن VIP" /></label>
    </form>
    <template #footer>
      <button class="btn" form="hall-form" :disabled="saving">ذخیره</button>
      <button class="btn secondary" type="button" @click="hallForm = null">انصراف</button>
    </template>
  </Modal>
</template>

<style scoped>
.cinema { margin-bottom: 16px; overflow: hidden; }
.chead { display: flex; align-items: center; gap: 12px; padding: 14px 16px; background: var(--surface-2); border-bottom: 1px solid var(--border); }
.chead h3 { margin: 0; }
.ico { width: 40px; height: 40px; border-radius: 10px; display: grid; place-items: center; background: var(--primary-soft); color: var(--sky); flex: none; }
.empty p { margin-bottom: 12px; }
@media (max-width: 640px) {
  .chead { flex-wrap: wrap; }
  .chead .grow { flex: 1 1 calc(100% - 64px); min-width: 0; }
  .chead .btn.sm { margin-inline-start: auto; }
  .list-row { flex-wrap: wrap; gap: 8px 10px; }
  .list-row > .grow { flex: 1 1 calc(100% - 40px); min-width: 0; }
  .list-row > .btn.sm { margin-inline-start: auto; }
}
</style>
