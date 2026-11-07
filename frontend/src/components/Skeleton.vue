<script setup>
// جای‌نگه‌دار اسکلتی برای همهٔ حالت‌های بارگذاری؛ شکل هر نوع شبیه صفحهٔ نهایی است تا جابه‌جایی چیدمان نداشته باشیم.
defineProps({ variant: { type: String, default: 'panels' } })
</script>

<template>
  <div class="skel" :class="variant" role="status" aria-busy="true" aria-label="در حال بارگذاری">
    <!-- صفحهٔ اول: اسلایدر + پوسترها -->
    <template v-if="variant === 'home'">
      <div class="sk hero" />
      <div class="sk line w30 h22" />
      <div class="grid posters"><div v-for="i in 8" :key="i" class="pc"><div class="sk poster" /><div class="sk line w80" /><div class="sk line w50 h10" /></div></div>
    </template>

    <!-- صفحهٔ فیلم -->
    <template v-else-if="variant === 'movie'">
      <div class="sk backdrop" />
      <div class="mrow">
        <div class="sk poster mp" />
        <div class="col"><div class="sk line w60 h26" /><div class="sk line w40" /><div class="row g"><div class="sk pill" /><div class="sk pill" /><div class="sk pill" /></div><div class="sk line" /><div class="sk line w90" /></div>
      </div>
      <div class="sk line w25 h20" />
      <div class="grid shows"><div v-for="i in 6" :key="i" class="sk card-sk" /></div>
    </template>

    <!-- انتخاب صندلی -->
    <template v-else-if="variant === 'seats'">
      <div class="sk line w40 h24" />
      <div class="seatsk">
        <div class="panel">
          <div class="sk screen" />
          <div v-for="r in 7" :key="r" class="seatrow"><span v-for="n in 12" :key="n" class="sk seat" /></div>
        </div>
        <div class="panel side"><div class="sk line w50 h20" /><div class="sk line" /><div class="sk line w80" /><div class="sk btn-sk" /></div>
      </div>
    </template>

    <!-- حساب من -->
    <template v-else-if="variant === 'account'">
      <div class="panel row g center"><div class="sk avatar" /><div class="col"><div class="sk line w40 h20" /><div class="sk line w25 h10" /></div></div>
      <div class="panel col g"><div class="sk line w30 h18" /><div v-for="i in 5" :key="i" class="row between"><div class="sk line w50" /><div class="sk toggle" /></div></div>
    </template>

    <!-- رزروهای من -->
    <template v-else-if="variant === 'tickets'">
      <div class="sk line w30 h24" />
      <div v-for="i in 3" :key="i" class="panel tk"><div class="sk poster tkp" /><div class="col grow"><div class="sk line w60 h20" /><div class="sk line w40" /><div class="row g"><div class="sk pill" /><div class="sk pill" /></div></div><div class="sk btn-sk sm" /></div>
    </template>

    <!-- پرداخت -->
    <template v-else-if="variant === 'pay'">
      <div class="panel col g"><div class="sk line w40 h22" /><div class="sk line w70" /><div class="sk line w50" /></div>
      <div class="panel col g center"><div class="sk cardimg" /><div class="sk btn-sk" /></div>
      <div class="panel col g"><div class="sk line w30 h18" /><div class="sk line" /><div class="sk drop" /></div>
    </template>

    <!-- داشبورد مدیریت -->
    <template v-else-if="variant === 'dashboard'">
      <div class="grid stats"><div v-for="i in 4" :key="i" class="panel col g"><div class="sk line w50 h10" /><div class="sk line w40 h26" /></div></div>
      <div class="panel col g"><div class="sk line w30 h20" /><div v-for="i in 4" :key="i" class="row g center"><div class="sk dotc" /><div class="sk line w60" /></div></div>
    </template>

    <!-- جدول -->
    <template v-else-if="variant === 'table'">
      <div class="row g"><div class="sk pill w120" /><div class="sk pill w120" /><div class="sk pill w120" /></div>
      <div class="panel col g"><div class="sk line h20" /><div v-for="i in 7" :key="i" class="sk line h34" /></div>
    </template>

    <!-- فرم -->
    <template v-else-if="variant === 'form'">
      <div v-for="p in 2" :key="p" class="panel col g"><div class="sk line w30 h20" /><div class="sk input" /><div class="row g"><div class="sk input grow" /><div class="sk input grow" /></div><div class="sk input" /></div>
    </template>

    <!-- پیش‌فرض: چند پنل -->
    <template v-else>
      <div class="row between"><div class="sk line w30 h24" /><div class="sk btn-sk sm" /></div>
      <div v-for="i in 3" :key="i" class="panel row g center"><div class="sk poster tkp" /><div class="col grow"><div class="sk line w50 h18" /><div class="sk line w30" /></div><div class="sk pill" /></div>
    </template>
  </div>
</template>

<style scoped>
.skel { display: grid; gap: 16px; align-content: start; }
.col { display: grid; gap: 10px; align-content: start; min-width: 0; }
.row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.row.g { gap: 10px; } .row.between { justify-content: space-between; } .row.center { align-items: center; }
.col.g { gap: 12px; } .grow { flex: 1; min-width: 0; }
.panel { background: var(--surface); border: 1px solid var(--border); border-radius: 14px; padding: 16px; }

/* بلوک اسکلتی با موج نور */
.sk {
  position: relative; overflow: hidden; border-radius: 8px; background: var(--surface-2);
}
.sk::after {
  content: ''; position: absolute; inset: 0; transform: translateX(-100%);
  background: linear-gradient(90deg, transparent, rgb(120 160 255 / 9%), transparent);
  animation: sweep 1.5s ease-in-out infinite;
}
@keyframes sweep { to { transform: translateX(100%); } }
@media (prefers-reduced-motion: reduce) { .sk::after { animation: none; } }

.line { height: 14px; border-radius: 7px; }
.h10 { height: 10px; } .h18 { height: 18px; } .h20 { height: 20px; } .h22 { height: 22px; } .h24 { height: 24px; } .h26 { height: 26px; } .h34 { height: 34px; border-radius: 9px; }
.w25 { width: 25%; } .w30 { width: 30%; } .w40 { width: 40%; } .w50 { width: 50%; } .w60 { width: 60%; } .w70 { width: 70%; } .w80 { width: 80%; } .w90 { width: 90%; } .w120 { width: 120px; }
.pill { height: 28px; width: 76px; border-radius: 999px; }
.input { height: 42px; border-radius: 10px; }
.btn-sk { height: 44px; width: 100%; border-radius: 12px; } .btn-sk.sm { width: 92px; height: 34px; }
.toggle { width: 42px; height: 24px; border-radius: 999px; }
.avatar { width: 56px; height: 56px; border-radius: 50%; }
.dotc { width: 14px; height: 14px; border-radius: 50%; }
.drop { height: 120px; border-radius: 12px; }
.cardimg { width: min(100%, 360px); aspect-ratio: 480 / 290; border-radius: 16px; }

.hero { height: clamp(220px, 46vw, 360px); border-radius: 18px; }
.grid { display: grid; gap: 14px; }
.posters { grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); }
.pc { display: grid; gap: 8px; }
.poster { aspect-ratio: 2 / 3; border-radius: 12px; }
.backdrop { height: clamp(160px, 36vw, 280px); border-radius: 16px; }
.mrow { display: flex; gap: 16px; align-items: flex-start; }
.mp { width: 110px; flex: none; }
.mrow .col { flex: 1; }
.shows { grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); }
.card-sk { height: 76px; border-radius: 12px; }
.stats { grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }

.seatsk { display: grid; gap: 16px; grid-template-columns: 1fr 300px; }
.screen { height: 26px; width: min(360px, 80%); margin: 0 auto 22px; border-radius: 50% 50% 0 0 / 100% 100% 0 0; }
.seatrow { display: flex; justify-content: center; gap: 7px; margin-bottom: 8px; }
.seat { width: 22px; height: 22px; border-radius: 6px; }
.side { display: grid; gap: 12px; align-content: start; }
.tk { display: flex; gap: 14px; align-items: center; }
.tkp { width: 52px; flex: none; border-radius: 9px; }
@media (max-width: 720px) {
  .seatsk { grid-template-columns: 1fr; }
  .seat { width: 18px; height: 18px; } .seatrow { gap: 5px; }
  .mp { width: 92px; }
}
</style>
