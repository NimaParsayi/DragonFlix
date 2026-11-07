<script setup>
import Skeleton from '../components/Skeleton.vue'
import { ref, computed, onMounted } from 'vue'
import Icon from '../components/Icon.vue'
import Switch from '../components/Switch.vue'
import PosterArt from '../components/PosterArt.vue'
import { api } from '../lib/api'
import { toast } from '../lib/ui'
import { genreList } from '../lib/genre'
import * as tg from '../lib/tg'

const me = ref(null)
const follows = ref([])
const error = ref('')

onMounted(async () => {
  try { ;[me.value, follows.value] = await Promise.all([api.get('/api/me'), api.get('/api/me/follows')]) }
  catch (e) { error.value = e.message }
})

const mandatory = computed(() => me.value?.categories.filter((c) => c.mandatory) ?? [])
const optional = computed(() => me.value?.categories.filter((c) => !c.mandatory) ?? [])

// با هر تغییر، فهرست کامل اعلان‌های روشن ذخیره می‌شود؛ اگر خطا شد به حالت قبل برمی‌گردد
async function toggle(c, value) {
  const before = c.enabled
  c.enabled = value
  tg.haptic.select()
  try { await api.put('/api/me/notifications', { enabled: optional.value.filter((x) => x.enabled).map((x) => x.key) }) }
  catch (e) { c.enabled = before; toast(e.message, 'error') }
}

async function unfollow(m) {
  try { await api.del(`/api/movies/${m.id}/follow`); follows.value = follows.value.filter((x) => x.id !== m.id); tg.haptic.tap() }
  catch (e) { toast(e.message, 'error') }
}
</script>

<template>
  <div v-if="error" class="alert error">{{ error }}</div>
  <Skeleton v-else-if="!me" variant="account" />

  <div v-else class="stack acc">
    <section class="card profile">
      <span class="avatar" aria-hidden="true">{{ me.user.fullName.slice(0, 1) }}</span>
      <div class="grow">
        <h2>{{ me.user.fullName }}</h2>
        <div class="muted small" dir="ltr" style="text-align: start">
          <template v-if="me.user.username">@{{ me.user.username }}</template><template v-else>ID {{ me.user.telegramId }}</template>
        </div>
      </div>
      <span v-if="me.user.role === 'Admin'" class="badge">ادمین</span>
    </section>

    <div v-if="!me.user.botReachable" class="alert error">
      <b>ربات رو بلاک کردی.</b> تا دوباره استارتش نکنی، اعلان‌ها (حتی بلیت) بهت نمی‌رسه.
      <button class="btn sm" style="margin-top: 8px" @click="tg.openTelegramLink(tg.botLink)">باز کردن ربات و Start</button>
    </div>

    <section class="card">
      <h3><Icon name="bell" :size="18" /> اعلان‌هایی که می‌خواهم</h3>
      <p class="muted small">اعلان‌ها توی همین ربات تلگرام برات میاد. هر کدوم رو خواستی روشن یا خاموش کن.</p>

      <div v-for="c in optional" :key="c.key" class="item">
        <div class="grow"><b>{{ c.title }}</b><div class="muted small">{{ c.description }}</div></div>
        <Switch :model-value="c.enabled" :label="c.title" @update:model-value="toggle(c, $event)" />
      </div>

      <h4 class="sub"><Icon name="lock" :size="15" /> ضروری — همیشه ارسال می‌شود</h4>
      <div v-for="c in mandatory" :key="c.key" class="item locked">
        <div class="grow"><b>{{ c.title }}</b><div class="muted small">{{ c.description }}</div></div>
        <Icon name="lock" :size="17" class="muted" />
      </div>
    </section>

    <section class="card">
      <h3><Icon name="film" :size="18" /> فیلم‌های دنبال‌شده</h3>
      <p v-if="!follows.length" class="muted small" style="margin: 0">هنوز فیلمی رو دنبال نمی‌کنی. توی صفحهٔ هر فیلم «دنبال کردن» رو بزن تا از سانس‌های جدیدش باخبر بشی.</p>
      <div v-for="m in follows" :key="m.id" class="item">
        <RouterLink :to="{ name: 'movie', params: { id: m.id } }" class="thumb"><PosterArt :movie="m" :show-title="false" /></RouterLink>
        <div class="grow"><RouterLink :to="{ name: 'movie', params: { id: m.id } }"><b>{{ m.title }}</b></RouterLink><div class="muted small">{{ genreList(m.genre).join(' · ') }}</div></div>
        <button class="btn secondary sm" @click="unfollow(m)"><Icon name="bellOff" :size="15" /> لغو</button>
      </div>
    </section>

    <RouterLink v-if="me.user.role === 'Admin'" to="/admin" class="btn secondary"><Icon name="dashboard" :size="17" /> پنل مدیریت</RouterLink>
  </div>
</template>

<style scoped>
.acc { max-width: 640px; margin-inline: auto; }
.profile { display: flex; align-items: center; gap: 14px; }
.profile h2 { margin: 0; }
.avatar { width: 52px; height: 52px; border-radius: 50%; display: grid; place-items: center; font-weight: 700; font-size: 1.2rem; background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; }
h3 { display: flex; align-items: center; gap: 8px; margin-bottom: 4px; }
.sub { display: flex; align-items: center; gap: 6px; margin: 18px 0 4px; color: var(--muted); font-size: .85rem; font-weight: 500; }
.item { display: flex; align-items: center; gap: 12px; padding: 12px 0; border-top: 1px solid var(--border); }
.item:first-of-type { border-top: 0; }
.item.locked { opacity: .8; }
.thumb { width: 38px; flex: none; border-radius: 6px; overflow: hidden; border: 1px solid var(--border); }
</style>
