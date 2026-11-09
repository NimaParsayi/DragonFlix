<script setup>
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../lib/api'
import { auth } from '../lib/auth'
import { toast } from '../lib/ui'
import Icon from '../components/Icon.vue'

const route = useRoute()
const router = useRouter()
const first = ref(auth.user?.firstName || '')
const last = ref(auth.user?.lastName || '')
const busy = ref(false)
const error = ref('')
const editing = computed(() => !!auth.user?.registered)
const valid = computed(() => first.value.trim().length >= 2 && last.value.trim().length >= 2)

async function save() {
  if (!valid.value || busy.value) return
  busy.value = true
  error.value = ''
  try {
    const r = await api.put('/api/me/profile', { firstName: first.value, lastName: last.value })
    auth.user = { ...auth.user, firstName: r.firstName, lastName: r.lastName, registered: true }
    toast('اطلاعاتت ثبت شد ✓')
    const next = typeof route.query.next === 'string' && route.query.next.startsWith('/') ? route.query.next : '/'
    router.replace(next)
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <form class="card reg" @submit.prevent="save">
    <span class="ic"><Icon name="user" :size="26" /></span>
    <h2>{{ editing ? 'ویرایش نام' : 'قبل از رزرو، خودت رو معرفی کن' }}</h2>
    <p class="muted">نام و نام خانوادگیت روی بلیت چاپ می‌شه و موقع ورود به سینما ازت می‌خوان. لطفاً درست وارد کن.</p>
    <label class="field">نام
      <input v-model="first" autocomplete="given-name" maxlength="30" placeholder="مثلاً علی" required />
    </label>
    <label class="field">نام خانوادگی
      <input v-model="last" autocomplete="family-name" maxlength="40" placeholder="مثلاً احمدی" required />
    </label>
    <p v-if="error" class="alert error">{{ error }}</p>
    <button class="btn lg" :disabled="!valid || busy">{{ busy ? 'در حال ثبت…' : editing ? 'ذخیره' : 'ثبت و ادامه' }}</button>
  </form>
</template>

<style scoped>
.reg { max-width: 420px; margin: 24px auto; display: grid; gap: 12px; padding: 24px 20px; }
.reg h2, .reg p { margin: 0; }
.ic { width: 52px; height: 52px; border-radius: 50%; display: grid; place-items: center; background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; justify-self: center; }
.reg h2 { text-align: center; }
.reg > p.muted { text-align: center; font-size: .9rem; }
</style>
