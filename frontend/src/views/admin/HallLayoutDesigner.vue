<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import SeatMap from '../../components/SeatMap.vue'
import SeatRowStrip from '../../components/SeatRowStrip.vue'
import Modal from '../../components/Modal.vue'
import Icon from '../../components/Icon.vue'
import { api } from '../../lib/api'
import { toast, confirmDialog } from '../../lib/ui'
import { num } from '../../lib/format'

const props = defineProps({ id: String })

const hall = ref(null)
const rows = ref([])
const loadError = ref('')
const saving = ref(false)
const dirty = ref(false)
const modal = ref(null) // 'bulk' | 'price'
const bulk = ref({ count: 5, seats: 12, price: 150000, offset: 0 })
const allPrice = ref(150000)

let key = 0
const withKey = (r) => ({ ...r, _k: ++key })

async function load() {
  try {
    const d = await api.get(`/api/admin/halls/${props.id}/layout`)
    hall.value = d.hall
    rows.value = d.rows.map((r) => withKey({ ...r, disabled: r.disabled ?? [] }))
    dirty.value = false
  } catch (e) { loadError.value = e.message }
}
const warn = (e) => { if (dirty.value) e.preventDefault() }
onMounted(() => { load(); window.addEventListener('beforeunload', warn) })
onBeforeUnmount(() => window.removeEventListener('beforeunload', warn))
onBeforeRouteLeave(() => !dirty.value || confirmDialog({ title: 'تغییرات ذخیره نشده', message: 'تغییرات این صفحه ذخیره نشده و از بین می‌رود. خارج می‌شوید؟', confirmText: 'خروج بدون ذخیره', danger: true }))

const touch = () => { dirty.value = true }

// A، B، … Z، AA، AB، …
function letters(i) {
  let s = ''
  for (i += 1; i > 0; i = Math.floor((i - 1) / 26)) s = String.fromCharCode(65 + ((i - 1) % 26)) + s
  return s
}
function nextLabel() {
  const used = new Set(rows.value.map((r) => r.label.toUpperCase()))
  for (let i = 0; ; i++) if (!used.has(letters(i))) return letters(i)
}
function newRow(base) {
  const last = rows.value.at(-1)
  return withKey({
    id: null, label: nextLabel(),
    seatCount: base?.seatCount ?? last?.seatCount ?? 10,
    price: base?.price ?? last?.price ?? 100000,
    offset: base?.offset ?? last?.offset ?? 0,
    aisleAfter: [...(base?.aisleAfter ?? last?.aisleAfter ?? [])],
    disabled: [],
  })
}
function addRow() { rows.value.push(newRow()); touch() }
function addBulk() {
  const n = Math.min(40 - rows.value.length, Math.max(0, Number(bulk.value.count)))
  for (let i = 0; i < n; i++) rows.value.push(newRow({ seatCount: Number(bulk.value.seats), price: Number(bulk.value.price), offset: Number(bulk.value.offset), aisleAfter: [] }))
  touch(); modal.value = null
}
function duplicate(i) { const r = newRow(rows.value[i]); rows.value.splice(i + 1, 0, r); touch() }
async function remove(i) {
  const r = rows.value[i]
  if (r.id && !(await confirmDialog({ title: 'حذف ردیف', message: `ردیف ${r.label} حذف شود؟ (با ذخیره‌کردن اعمال می‌شود)`, confirmText: 'حذف', danger: true }))) return
  rows.value.splice(i, 1); touch()
}
function move(i, d) {
  const j = i + d
  if (j < 0 || j >= rows.value.length) return
  ;[rows.value[i], rows.value[j]] = [rows.value[j], rows.value[i]]
  touch()
}
function toggleAisle(r, n) {
  const s = new Set(r.aisleAfter)
  s.has(n) ? s.delete(n) : s.add(n)
  r.aisleAfter = [...s].sort((a, b) => a - b)
  touch()
}
function toggleDisabled(r, n) {
  const s = new Set(r.disabled)
  s.has(n) ? s.delete(n) : s.add(n)
  r.disabled = [...s].sort((a, b) => a - b)
  touch()
}
function onSeatCount(r) {
  r.aisleAfter = r.aisleAfter.filter((a) => a < r.seatCount)
  r.disabled = r.disabled.filter((a) => a <= r.seatCount)
  touch()
}
function applyAllPrice() {
  rows.value.forEach((r) => (r.price = Number(allPrice.value)))
  touch(); modal.value = null
}

const totalSeats = computed(() => rows.value.reduce((s, r) => s + (Number(r.seatCount) || 0) - r.disabled.length, 0))
const preview = computed(() => rows.value.map((r) => ({ ...r, seatCount: Number(r.seatCount) || 0, offset: Number(r.offset) || 0, price: Number(r.price) || 0 })))

async function save() {
  saving.value = true
  try {
    await api.put(`/api/admin/halls/${props.id}/layout`, {
      rows: rows.value.map((r) => ({
        id: r.id, label: r.label, seatCount: Number(r.seatCount), price: Number(r.price),
        offset: Number(r.offset), aisleAfter: r.aisleAfter, disabled: r.disabled,
      })),
    })
    await load()
    toast('چینش سالن ذخیره شد.')
  } catch (e) { toast(e.message, 'error') } finally { saving.value = false }
}
</script>

<template>
  <div v-if="!hall && loadError" class="alert error">{{ loadError }}</div>
  <Skeleton v-else-if="!hall" variant="seats" />

  <template v-else>
    <nav class="crumbs" aria-label="مسیر">
      <RouterLink :to="{ name: 'admin-cinemas' }">سینماها و سالن‌ها</RouterLink> ›
      <span>{{ hall.cinema }}</span> › <b>{{ hall.name }}</b>
    </nav>

    <div class="bar">
      <div>
        <h1>چینش صندلی‌های {{ hall.name }}</h1>
        <span class="muted small">{{ num(rows.length) }} ردیف · {{ num(totalSeats) }} صندلی قابل فروش</span>
      </div>
      <div class="row">
        <span v-if="dirty" class="badge warn">ذخیره‌نشده</span>
        <button class="btn secondary" :disabled="!dirty || saving" @click="load">لغو تغییرات</button>
        <button class="btn" :disabled="!dirty || saving" @click="save">{{ saving ? 'در حال ذخیره…' : 'ذخیرهٔ چینش' }}</button>
      </div>
    </div>

    <div class="work">
      <section class="left">
        <div class="tools">
          <button class="btn secondary sm" @click="addRow"><Icon name="plus" :size="15" /> یک ردیف</button>
          <button class="btn secondary sm" @click="modal = 'bulk'"><Icon name="plus" :size="15" /> چند ردیف یکجا…</button>
          <button class="btn secondary sm" :disabled="!rows.length" @click="allPrice = rows[0]?.price; modal = 'price'"><Icon name="money" :size="15" /> قیمت یکسان برای همه…</button>
        </div>

        <div v-if="!rows.length" class="panel empty">
          <p>این سالن هنوز ردیفی ندارد.</p>
          <button class="btn" @click="modal = 'bulk'">ساخت ردیف‌ها</button>
        </div>

        <article v-for="(r, i) in rows" :key="r._k" class="panel rowcard">
          <div class="rhead">
            <label class="f lab">ردیف<input v-model="r.label" maxlength="4" required @input="touch" /></label>
            <label class="f">صندلی<input v-model.number="r.seatCount" type="number" min="1" max="60" @input="onSeatCount(r)" /></label>
            <label class="f price">قیمت (تومان)<input v-model.number="r.price" type="number" min="0" step="1000" @input="touch" /></label>
            <label class="f" title="تعداد خانهٔ خالی ابتدای ردیف">فاصله<input v-model.number="r.offset" type="number" min="0" max="30" @input="touch" /></label>
            <div class="tools-r">
              <button class="iconbtn" :disabled="i === 0" title="بالاتر (نزدیک‌تر به پرده)" aria-label="بالاتر" @click="move(i, -1)"><Icon name="up" :size="17" /></button>
              <button class="iconbtn" :disabled="i === rows.length - 1" title="پایین‌تر" aria-label="پایین‌تر" @click="move(i, 1)"><Icon name="down" :size="17" /></button>
              <button class="iconbtn" title="کپی ردیف" aria-label="کپی ردیف" @click="duplicate(i)"><Icon name="copy" :size="17" /></button>
              <button class="iconbtn danger" title="حذف ردیف" aria-label="حذف ردیف" @click="remove(i)"><Icon name="trash" :size="17" /></button>
            </div>
          </div>
          <div class="strip-scroll">
            <SeatRowStrip :row="{ ...r, seatCount: Number(r.seatCount) || 0 }" mode="edit" @toggle-aisle="(n) => toggleAisle(r, n)" @toggle-disabled="(n) => toggleDisabled(r, n)" />
          </div>
        </article>
      </section>

      <aside class="right">
        <div class="panel preview">
          <h3>پیش‌نمایش سالن</h3>
          <SeatMap :rows="preview" mode="view" compact />
        </div>
        <div class="panel help">
          <h3>راهنما</h3>
          <ul>
            <li><b>ردیف اول</b> نزدیک‌ترین ردیف به پرده است.</li>
            <li>روی <b>فاصلهٔ بین دو صندلی</b> کلیک کنید تا راهرو بسازید یا بردارید.</li>
            <li>روی <b>خود صندلی</b> کلیک کنید تا غیرفعال شود (خراب، ستون، …). صندلی غیرفعال فروخته نمی‌شود.</li>
            <li><b>فاصله</b> یعنی چند خانهٔ خالی ابتدای ردیف (برای ردیف‌های کوتاه‌تر وسط).</li>
          </ul>
        </div>
      </aside>
    </div>

    <Modal v-if="modal === 'bulk'" title="افزودن چند ردیف" width="420px" @close="modal = null">
      <form id="bulk-form" class="two" @submit.prevent="addBulk">
        <label class="field">تعداد ردیف<input v-model.number="bulk.count" type="number" min="1" max="40" required /></label>
        <label class="field">صندلی در هر ردیف<input v-model.number="bulk.seats" type="number" min="1" max="60" required /></label>
        <label class="field">قیمت هر صندلی (تومان)<input v-model.number="bulk.price" type="number" min="0" step="1000" required /></label>
        <label class="field">فاصلهٔ ابتدای ردیف<input v-model.number="bulk.offset" type="number" min="0" max="30" /></label>
      </form>
      <template #footer>
        <button class="btn" form="bulk-form">افزودن {{ num(bulk.count || 0) }} ردیف</button>
        <button class="btn secondary" type="button" @click="modal = null">انصراف</button>
      </template>
    </Modal>

    <Modal v-if="modal === 'price'" title="قیمت یکسان برای همهٔ ردیف‌ها" width="380px" @close="modal = null">
      <form id="price-form" @submit.prevent="applyAllPrice">
        <label class="field">قیمت هر صندلی (تومان)<input v-model.number="allPrice" type="number" min="0" step="1000" required autofocus /></label>
      </form>
      <template #footer>
        <button class="btn" form="price-form">اعمال</button>
        <button class="btn secondary" type="button" @click="modal = null">انصراف</button>
      </template>
    </Modal>
  </template>
</template>

<style scoped>
.crumbs { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; font-size: .88rem; color: var(--muted); margin-bottom: 8px; }
.bar { position: sticky; top: 57px; z-index: 10; display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; padding: 12px 0; margin-bottom: 14px; background: var(--bg); border-bottom: 1px solid var(--border); }
.bar h1 { margin: 0; font-size: 1.25rem; }

.work { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(0, 1fr); gap: 20px; align-items: start; }
.left { display: grid; gap: 12px; }
.tools { display: flex; gap: 8px; flex-wrap: wrap; }
.right { position: sticky; top: 140px; display: grid; gap: 14px; }
.preview, .help { padding: 16px; }
.preview h3, .help h3 { font-size: .95rem; margin-bottom: 10px; }
.help ul { margin: 0; padding-inline-start: 18px; display: grid; gap: 6px; color: var(--muted); font-size: .88rem; }
.help b { color: var(--text); font-weight: 500; }

.empty { padding: 28px; text-align: center; }
.empty p { color: var(--muted); }

.rowcard { padding: 12px 14px; display: grid; gap: 10px; }
.rhead { display: grid; grid-template-columns: 60px 76px minmax(110px, 1fr) 76px auto; gap: 10px; align-items: end; }
.f { display: grid; gap: 3px; font-size: .75rem; color: var(--muted); }
.f input { padding: 6px 10px; }
.tools-r { display: flex; gap: 2px; }
.strip-scroll { --sm-seat: 28px; direction: ltr; overflow-x: auto; padding: 8px 0; background: var(--surface-2); border-radius: 10px; }
.two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }

@media (max-width: 1000px) {
  .work { grid-template-columns: minmax(0, 1fr); }
  .right { position: static; order: -1; }
}
@media (max-width: 640px) {
  .rhead { grid-template-columns: 1fr 1fr; }
  .rhead .price, .tools-r { grid-column: 1 / -1; }
  .bar { position: static; }
}
</style>
