<script setup>
// لوگوی متنی: «دراگون» و «فلیکس» جدا از هم با فونت تزئینی، و شعار زیر آن
defineProps({ tagline: { type: Boolean, default: true }, size: { type: String, default: 'md' } })
</script>

<template>
  <span class="wordmark" :class="size">
    <span class="name"><span class="w1">دراگون</span><span class="w2">فلیکس</span></span>
    <small v-if="tagline" class="tag">اکران اختصاصی فیلم‌های خارجی در شیراز</small>
  </span>
</template>

<style scoped>
.wordmark { display: inline-flex; flex-direction: column; line-height: 1.15; }
.name { font-family: 'Lalezar', 'Vazirmatn', sans-serif; font-weight: 400; font-size: 1.7rem; display: inline-flex; gap: .28em; letter-spacing: 0; }
.w1 { color: var(--text); }
.w2 { background: linear-gradient(180deg, #93c5fd, #3b82f6); -webkit-background-clip: text; background-clip: text; color: transparent; filter: drop-shadow(0 0 10px rgb(59 130 246 / 35%)); }
.tag { font-size: .66rem; color: var(--muted); font-weight: 400; letter-spacing: 0; margin-top: 1px; }
.lg { align-items: center; text-align: center; }
.lg .name { font-size: 2.8rem; }
.lg .tag { font-size: .9rem; margin-top: 4px; }
@media (max-width: 720px) { .md .name { font-size: 1.5rem; } }
</style>
