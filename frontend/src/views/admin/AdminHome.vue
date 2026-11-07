<script setup>
import Skeleton from '../../components/Skeleton.vue'
import { ref, computed, onMounted } from 'vue'
import Icon from '../../components/Icon.vue'
import { api } from '../../lib/api'
import { num, money, fmtDate, fmtTime } from '../../lib/format'

const s = ref(null)
const error = ref('')
onMounted(async () => {
  try { s.value = await api.get('/api/admin/stats') } catch (e) { error.value = e.message }
})

// مراحل راه‌اندازی به همان ترتیبی که باید انجام شوند
const steps = computed(() => {
  const d = s.value
  if (!d) return []
  return [
    { done: d.cinemas > 0 && d.halls > 0, title: 'سینما و سالن تعریف کنید', hint: `${num(d.cinemas)} سینما · ${num(d.halls)} سالن`, to: 'admin-cinemas' },
    { done: d.halls > 0 && d.hallsWithoutLayout === 0, title: 'چینش صندلی‌های هر سالن را بسازید', hint: d.hallsWithoutLayout ? `${num(d.hallsWithoutLayout)} سالن هنوز چینش ندارد` : 'همهٔ سالن‌ها چینش دارند', to: 'admin-cinemas' },
    { done: d.movies > 0, title: 'فیلم اضافه کنید', hint: `${num(d.movies)} فیلم`, to: 'admin-movies' },
    { done: d.upcomingShowtimes > 0, title: 'سانس پیش‌رو ثبت کنید', hint: `${num(d.upcomingShowtimes)} سانس پیش رو`, to: 'admin-showtimes' },
    { done: d.paymentConfigured, title: 'شمارهٔ کارت پرداخت را ثبت کنید', hint: d.paymentConfigured ? 'ثبت شده' : 'بدون آن کاربران نمی‌توانند رزرو کنند', to: 'admin-settings' },
    { done: d.botReady && d.webAppReady, title: 'آدرس HTTPS مینی‌اپ را به ربات بدهید', hint: !d.botReady ? 'توکن ربات تنظیم نشده' : d.webAppReady ? 'تنظیم شده' : 'تا آدرس HTTPS نباشد دکمهٔ مینی‌اپ در ربات ظاهر نمی‌شود', to: 'admin-settings' },
  ]
})
const allDone = computed(() => steps.value.length > 0 && steps.value.every((x) => x.done))
const pct = (x) => (x.capacity ? Math.min(100, Math.round((x.booked / x.capacity) * 100)) : 0)
</script>

<template>
  <div class="page-head">
    <div><h1>نمای کلی</h1><p class="muted">وضعیت امروز سینماها و رزروها.</p></div>
    <RouterLink class="btn" :to="{ name: 'admin-tickets' }"><Icon name="qr" :size="17" /> بررسی بلیت</RouterLink>
  </div>

  <div v-if="error" class="alert error">{{ error }}</div>
  <Skeleton v-else-if="!s" variant="dashboard" />

  <template v-else>
    <RouterLink v-if="s.pendingReview" :to="{ name: 'admin-payments' }" class="alertcard">
      <Icon name="card" :size="22" />
      <div class="grow"><b>{{ num(s.pendingReview) }} رسید منتظر بررسی شماست</b><div class="small">برای تأیید و صدور بلیت بزنید</div></div>
      <Icon name="back" :size="18" style="transform: rotate(180deg)" />
    </RouterLink>

    <section v-if="!allDone" class="panel setup">
      <h3>راه‌اندازی سایت</h3>
      <p class="muted small">این مراحل را به ترتیب انجام دهید تا کاربران بتوانند رزرو کنند.</p>
      <ol>
        <li v-for="(st, i) in steps" :key="i" :class="{ done: st.done }">
          <span class="mark">{{ st.done ? '✓' : num(i + 1) }}</span>
          <div class="grow"><b>{{ st.title }}</b><span class="muted small">{{ st.hint }}</span></div>
          <RouterLink v-if="!st.done" class="btn sm" :to="{ name: st.to }">انجام بده</RouterLink>
        </li>
      </ol>
    </section>

    <div class="stats">
      <RouterLink class="panel stat" :to="{ name: 'admin-sales' }"><span class="muted small">رزروهای امروز</span><b>{{ num(s.bookingsToday) }}</b></RouterLink>
      <RouterLink class="panel stat" :to="{ name: 'admin-sales', query: { status: 'used' } }"><span class="muted small">ورود ثبت‌شده امروز</span><b>{{ num(s.checkedInToday) }}</b></RouterLink>
      <RouterLink class="panel stat" :to="{ name: 'admin-sales' }"><span class="muted small">کل رزروها</span><b>{{ num(s.bookingsTotal) }}</b></RouterLink>
      <RouterLink class="panel stat" :to="{ name: 'admin-sales' }"><span class="muted small">مجموع فروش</span><b class="money">{{ money(s.revenueTotal) }}</b></RouterLink>
    </div>

    <section class="panel" style="margin-top: 20px">
      <div class="phead">
        <h3>سانس‌های پیش رو</h3>
        <RouterLink :to="{ name: 'admin-showtimes' }" class="small">همهٔ سانس‌ها</RouterLink>
      </div>
      <div v-if="!s.next.length" class="empty">سانس پیش‌رویی ثبت نشده است.</div>
      <div v-for="x in s.next" :key="x.id" class="list-row">
        <div class="when"><b>{{ fmtTime(x.startsAt) }}</b><span class="muted small">{{ fmtDate(x.startsAt) }}</span></div>
        <div class="grow"><b>{{ x.movie }}</b><div class="muted small">{{ x.cinema }} · {{ x.hall }}</div></div>
        <RouterLink class="occ" :to="{ name: 'admin-sales', query: { showtimeId: x.id } }" title="بلیت‌های فروخته‌شدهٔ این سانس">
          <span class="small muted">{{ num(x.booked) }} از {{ num(x.capacity) }} صندلی</span>
          <div class="meter"><i :style="{ width: pct(x) + '%' }" /></div>
        </RouterLink>
      </div>
    </section>
  </template>
</template>

<style scoped>
.alertcard { display: flex; align-items: center; gap: 14px; padding: 14px 18px; margin-bottom: 16px; border-radius: 12px; color: #f0b952; background: #261e0b; border: 1px solid #5a4514; }
.alertcard:hover { text-decoration: none; border-color: #8a6a1d; }
.alertcard b { color: #f5d98a; }
.setup { padding: 20px; margin-bottom: 20px; }
.setup h3 { margin-bottom: 2px; }
.setup ol { list-style: none; padding: 0; margin: 14px 0 0; display: grid; gap: 8px; }
.setup li { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 10px; background: var(--surface-2); }
.setup li > div { display: grid; line-height: 1.5; }
.setup li.done { opacity: .65; }
.mark { width: 28px; height: 28px; border-radius: 50%; display: grid; place-items: center; flex: none; font-weight: 700; font-size: .85rem; border: 1px solid #1f3a68; color: var(--sky); }
.done .mark { background: #0f2619; border-color: #1d4a2e; color: #86efac; }

.stats { display: grid; gap: 12px; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }
.stat { padding: 16px 18px; display: grid; gap: 2px; color: var(--text); transition: border-color .15s; }
a.stat:hover { border-color: #34405a; text-decoration: none; }
.stat b { font-size: 1.6rem; line-height: 1.3; }
.stat b.money { font-size: 1.15rem; padding-top: 6px; }

.phead { display: flex; justify-content: space-between; align-items: center; padding: 14px 16px; border-bottom: 1px solid var(--border); }
.phead h3 { margin: 0; }
.when { display: grid; line-height: 1.4; min-width: 78px; }
.when b { font-size: 1.1rem; color: var(--sky); }
.list-row > .grow { min-width: 0; }
.occ { display: grid; gap: 4px; min-width: 150px; color: inherit; }
.occ:hover { text-decoration: none; }
.occ:hover span { color: var(--sky); }
@media (max-width: 640px) {
  .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
  .stat { padding: 12px 14px; }
  .stat b { font-size: 1.35rem; }
  .occ { min-width: 0; } .occ .meter { display: none; }
}
</style>
