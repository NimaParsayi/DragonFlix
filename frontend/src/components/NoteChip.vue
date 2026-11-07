<script setup>
import Icon from './Icon.vue'
defineProps({ note: { type: String, default: '' }, size: { type: String, default: 'md' } })
</script>

<template>
  <span v-if="note" class="notechip" :class="size"><Icon name="sparkles" :size="size === 'sm' ? 13 : 15" /> {{ note }}</span>
</template>

<style scoped>
/* توضیح ویژهٔ فیلم (مثلاً «با دوبله محبوب سورن») در همهٔ بخش‌های سایت یک ظاهر دارد */
.notechip { display: inline-flex; align-items: center; gap: 7px; padding: 3px 14px; border-radius: 999px; font-size: .9rem; font-weight: 500; line-height: 1.6;
  background: rgb(240 185 82 / 12%); color: #f0b952; border: 1px solid #5a4514; }
.notechip.sm { padding: 1px 10px; font-size: .8rem; }
</style>
