<script setup>
import { computed } from 'vue'
import { nav } from './lib/nav'
import { useRoute } from 'vue-router'
import { auth } from './lib/auth'
import UiHost from './components/UiHost.vue'
import Icon from './components/Icon.vue'
import Skeleton from './components/Skeleton.vue'

const route = useRoute()
// صفحهٔ اول در موبایل: هدر شفاف روی اسلایدر می‌نشیند و با آن یک بلوک می‌شود
const overlay = computed(() => route.name === 'home')
// تب فعال بلافاصله بعد از لمس عوض می‌شود، نه بعد از لود صفحهٔ مقصد
const cur = computed(() => nav.to?.path ?? route.path)
const isOn = (p) => (p === '/' ? cur.value === '/' : cur.value === p || cur.value.startsWith(p + '/'))
const initial = computed(() => (auth.user?.fullName || '؟').trim().slice(0, 1))
</script>

<template>
  <div class="navbar" :class="{ on: nav.loading }" aria-hidden="true"><i /></div>
  <header class="top" :class="{ overlay }">
    <div class="container bar">
      <RouterLink to="/" class="brand">
        <img class="logo" src="/logo-96.jpg" width="42" height="42" alt="" />
        <span>دراگون<b>فلیکس</b></span>
      </RouterLink>

      <nav v-if="auth.token" class="desk">
        <RouterLink to="/" exact-active-class="on">فیلم‌ها</RouterLink>
        <RouterLink to="/my-bookings" active-class="on">رزروهای من</RouterLink>
        <RouterLink v-if="auth.isAdmin" to="/admin" active-class="on">مدیریت</RouterLink>
      </nav>

      <RouterLink v-if="auth.user" to="/me" class="who" title="حساب من">
        <span class="name">{{ auth.user.fullName }}</span>
        <span class="avatar" aria-hidden="true">{{ initial }}</span>
      </RouterLink>
    </div>
  </header>

  <main class="container page" :class="{ overlay }">
    <Skeleton v-if="!auth.ready" :variant="overlay ? 'home' : 'panels'" />
    <RouterView v-else />
  </main>

  <UiHost />

  <!-- موبایل: نوار پایین (روی دسکتاپ پنهان است) -->
  <nav v-if="(auth.token || !auth.ready) && !route.meta.flow" class="tabbar" aria-label="ناوبری اصلی">
    <RouterLink to="/" class="tab" :class="{ on: isOn('/') }"><span class="ti"><Icon name="film" :size="21" /></span><span class="tl">فیلم‌ها</span></RouterLink>
    <RouterLink to="/my-bookings" class="tab" :class="{ on: isOn('/my-bookings') }"><span class="ti"><Icon name="ticket" :size="21" /></span><span class="tl">رزروهای من</span></RouterLink>
    <RouterLink v-if="auth.isAdmin" to="/admin" class="tab" :class="{ on: isOn('/admin') }"><span class="ti"><Icon name="dashboard" :size="21" /></span><span class="tl">مدیریت</span></RouterLink>
    <RouterLink to="/me" class="tab" :class="{ on: isOn('/me') }"><span class="ti"><Icon name="user" :size="21" /></span><span class="tl">حساب من</span></RouterLink>
  </nav>

</template>

<style scoped>
.navbar { position: fixed; inset-inline: 0; top: 0; height: 3px; z-index: 300; pointer-events: none; opacity: 0; transition: opacity .25s .1s; overflow: hidden; }
.navbar.on { opacity: 1; }
.navbar i { position: absolute; inset-block: 0; width: 40%; background: linear-gradient(90deg, transparent, var(--primary), #60a5fa, transparent); animation: nb 1s ease-in-out infinite; }
@keyframes nb { from { inset-inline-start: -40%; } to { inset-inline-start: 100%; } }
.top { padding-top: var(--safe-top); position: sticky; top: 0; z-index: 20; background: var(--bg); border-bottom: 1px solid var(--border); }
.bar { display: flex; align-items: center; gap: 22px; padding-block: 10px; flex-wrap: wrap; }
.brand { display: inline-flex; align-items: center; gap: 10px; color: var(--text); font-weight: 500; font-size: 1.15rem; }
.brand b { color: var(--sky); font-weight: 700; }
.brand:hover { text-decoration: none; }
.logo { border-radius: 50%; display: block; flex: none; object-fit: cover; }
nav.desk { display: flex; gap: 4px; flex: 1; }
nav.desk a { color: var(--muted); padding: 5px 14px; border-radius: 8px; transition: background .15s, color .15s; }
nav.desk a:hover { background: var(--surface-2); text-decoration: none; color: var(--text); }
nav.desk a.on { background: var(--primary-soft); color: #9cc2ff; }
.who { display: flex; align-items: center; gap: 10px; color: inherit; }
.who:hover { text-decoration: none; }
.splash { display: grid; justify-items: center; gap: 12px; padding: 90px 0; }
.splash img { border-radius: 50%; animation: pulse 1.4s ease-in-out infinite; }
@keyframes pulse { 50% { opacity: .5; transform: scale(.96); } }
.avatar { width: 30px; height: 30px; border-radius: 50%; display: grid; place-items: center; font-weight: 700; font-size: .85rem; background: var(--primary-soft); color: #9cc2ff; border: 1px solid #1f3a68; }
.name { font-size: .9rem; color: var(--muted); }

.tabbar { display: none; }

@media (max-width: 720px) {
  /* هدر موبایل فقط برند است؛ ناوبری در نوار شناور پایین */
  .bar { flex-wrap: nowrap; justify-content: space-between; gap: 12px; padding-block: 8px; }
  nav.desk, .name { display: none; }
  .brand { font-size: 1.05rem; }

  /* صفحهٔ اول: هدر شفاف روی اسلایدر (یک بلوک یکپارچه) */
  .top.overlay {
    position: absolute; inset-inline: 0; top: 0; z-index: 25; border-bottom: 0;
    padding-top: var(--safe-top);
    background: linear-gradient(180deg, rgb(11 13 18 / 78%), rgb(11 13 18 / 0%));
  }
  .top.overlay .brand { text-shadow: 0 1px 8px rgb(0 0 0 / 55%); }
  .top.overlay .avatar { background: rgb(11 13 18 / 55%); backdrop-filter: blur(6px); }
  main.overlay { padding-top: 0; }

  /* نوار پایین: قرص شناور با فاصله از لبه‌ها */
  .tabbar {
    display: grid; grid-auto-flow: column; grid-auto-columns: 1fr; gap: 2px;
    position: fixed; z-index: 30;
    inset-inline: 14px; bottom: calc(12px + var(--safe-bottom));
    padding: 6px; border-radius: 24px;
    background: rgb(22 26 35 / 82%); backdrop-filter: blur(18px) saturate(1.4);
    border: 1px solid rgb(255 255 255 / 9%);
    box-shadow: 0 10px 30px rgb(0 0 0 / 55%), inset 0 1px 0 rgb(255 255 255 / 5%);
  }
  .tab {
    display: grid; justify-items: center; gap: 1px; padding: 3px 2px 4px; color: var(--muted); border-radius: 18px;
    font: inherit; background: none; border: 0; cursor: pointer; -webkit-tap-highlight-color: transparent;
  }
  .tab:hover { text-decoration: none; }
  .ti { display: grid; place-items: center; width: 52px; height: 30px; border-radius: 15px; transition: background .2s, color .2s, transform .15s; }
  .tl { font-size: .68rem; line-height: 1.5; transition: color .2s; }
  .tab:active .ti { transform: scale(.92); }
  .tab.on { color: #cfe0ff; }
  .tab.on .ti { background: var(--primary); color: #fff; box-shadow: 0 4px 14px rgb(59 130 246 / 40%); }

  main.page { padding-bottom: calc(104px + var(--safe-bottom)); }
}
</style>
