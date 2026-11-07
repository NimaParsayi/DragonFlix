<script setup>
import { computed } from 'vue'
import { ageLabel } from '../lib/genre'

const props = defineProps({ code: { type: String, default: '' } })
const label = computed(() => ageLabel(props.code))
</script>

<template>
  <span v-if="label" class="age" :class="`a-${code}`" :title="code === 'all' ? 'مناسب همه سنین' : `مناسب ${code} سال به بالا`">{{ label }}</span>
</template>

<style scoped>
.age { display: inline-grid; place-items: center; min-width: 30px; height: 22px; padding: 0 7px; border-radius: 6px; font-size: .75rem; font-weight: 700; border: 1px solid; line-height: 1; }
.a-all { background: #0f2619; color: #86efac; border-color: #1d4a2e; }
.a-7 { background: var(--primary-soft); color: #9cc2ff; border-color: #1f3a68; }
.a-12 { background: #33270e; color: #f0b952; border-color: #5a4514; }
.a-15 { background: #33200e; color: #fb923c; border-color: #6b3a14; }
.a-18 { background: #2a1417; color: #fca5a5; border-color: #6b2a30; }
</style>
