<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { adminBadge } from '../../lib/adminBadge'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'

const route = useRoute()
// تعداد رسیدهای منتظر تأیید روی منو نشان داده می‌شود
let timer
onMounted(() => { adminBadge.refresh(); timer = setInterval(() => !document.hidden && adminBadge.refresh(), 30000) })
onBeforeUnmount(() => clearInterval(timer))

// منو به ترتیب کار: اول ساختار، بعد برنامهٔ نمایش، بعد کار روزانه
const groups = [
  { title: null, items: [{ to: 'admin-home', label: 'نمای کلی', icon: 'dashboard' }] },
  { title: 'ساختار', items: [{ to: 'admin-cinemas', label: 'سینماها و سالن‌ها', icon: 'building', also: ['admin-layout'] }] },
  { title: 'برنامهٔ نمایش', items: [
    { to: 'admin-movies', label: 'فیلم‌ها', icon: 'film' },
    { to: 'admin-showtimes', label: 'سانس‌ها', icon: 'calendar' },
    { to: 'admin-slides', label: 'اسلایدر صفحهٔ اول', icon: 'sparkles' },
  ] },
  { title: 'فروش و ورودی', items: [
    { to: 'admin-payments', label: 'پرداخت‌ها', icon: 'card', badge: true },
    { to: 'admin-sales', label: 'بلیت‌های فروخته‌شده', icon: 'ticket' },
    { to: 'admin-tickets', label: 'بررسی بلیت', icon: 'qr' },
  ] },
  { title: 'ارتباط و تنظیمات', items: [
    { to: 'admin-broadcast', label: 'اعلان همگانی', icon: 'megaphone' },
    { to: 'admin-settings', label: 'پرداخت و ربات', icon: 'settings' },
    { to: 'admin-backup', label: 'پشتیبان‌گیری', icon: 'download' },
  ] },
]
const active = (it) => route.name === it.to || it.also?.includes(route.name)
// موبایل: به‌جای نوار اسکرولی، یک دکمهٔ «بخش فعلی» که منوی کامل را به‌صورت برگهٔ پایین باز می‌کند
const menuOpen = ref(false)
const current = computed(() => groups.flatMap((g) => g.items).find(active) || groups[0].items[0])
watch(() => route.name, () => { menuOpen.value = false })
</script>

<template>
  <div class="admin">
    <button class="menubtn" type="button" aria-haspopup="dialog" @click="menuOpen = true">
      <Icon :name="current.icon" :size="18" /> <span>{{ current.label }}</span>
      <b v-if="adminBadge.pending && current.to !== 'admin-payments'" class="bd">{{ adminBadge.pending }}</b>
      <Icon name="down" :size="16" class="chev" />
    </button>
    <Modal v-if="menuOpen" title="منوی مدیریت" @close="menuOpen = false">
      <nav class="sheetnav" aria-label="مدیریت">
        <div v-for="(g, i) in groups" :key="i" class="group">
          <div v-if="g.title" class="gt">{{ g.title }}</div>
          <RouterLink v-for="it in g.items" :key="it.to" :to="{ name: it.to }" class="item" :class="{ on: active(it) }">
            <Icon :name="it.icon" :size="18" /> {{ it.label }}<b v-if="it.badge && adminBadge.pending" class="bd">{{ adminBadge.pending }}</b>
          </RouterLink>
        </div>
      </nav>
    </Modal>
    <aside class="side">
      <nav aria-label="مدیریت">
        <div v-for="(g, i) in groups" :key="i" class="group">
          <div v-if="g.title" class="gt">{{ g.title }}</div>
          <RouterLink v-for="it in g.items" :key="it.to" :to="{ name: it.to }" class="item" :class="{ on: active(it) }">
            <Icon :name="it.icon" :size="18" /> {{ it.label }}<b v-if="it.badge && adminBadge.pending" class="bd">{{ adminBadge.pending }}</b>
          </RouterLink>
        </div>
      </nav>
    </aside>
    <section class="content"><RouterView /></section>
  </div>
</template>

<style scoped>
.admin { display: grid; grid-template-columns: 220px minmax(0, 1fr); gap: 32px; align-items: start; }
.side { position: sticky; top: 76px; }
.group { margin-bottom: 18px; }
.gt { font-size: .75rem; color: var(--muted); padding: 0 12px 6px; letter-spacing: .02em; }
.item { display: flex; align-items: center; gap: 10px; padding: 9px 12px; border-radius: 10px; color: var(--muted); transition: background .15s, color .15s; }
.item:hover { background: var(--surface-2); color: var(--text); text-decoration: none; }
.item.on { background: var(--primary-soft); color: #9cc2ff; font-weight: 500; }
.content { min-width: 0; }
.bd { margin-inline-start: auto; min-width: 20px; height: 20px; border-radius: 999px; display: inline-grid; place-items: center; font-size: .72rem; background: var(--primary); color: #fff; padding: 0 6px; }

.menubtn { display: none; }
.sheetnav .group { margin-bottom: 10px; }
.sheetnav .item { padding: 12px; }

@media (max-width: 860px) {
  .admin { grid-template-columns: minmax(0, 1fr); gap: 14px; }
  .side { display: none; }
  .menubtn { display: flex; align-items: center; gap: 10px; width: 100%; padding: 11px 14px; border-radius: 12px; border: 1px solid var(--border); background: var(--surface); color: var(--text); font: inherit; font-weight: 500; cursor: pointer; }
  .menubtn .chev { margin-inline-start: auto; color: var(--muted); }
  .menubtn .bd { margin-inline-start: 0; }
}
</style>
