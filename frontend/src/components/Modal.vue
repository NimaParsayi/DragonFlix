<script setup>
import { onMounted, onBeforeUnmount } from 'vue'

const props = defineProps({ title: String, width: { type: String, default: '480px' } })
const emit = defineEmits(['close'])
const onKey = (e) => e.key === 'Escape' && emit('close')
onMounted(() => { document.addEventListener('keydown', onKey); document.body.style.overflow = 'hidden' })
onBeforeUnmount(() => { document.removeEventListener('keydown', onKey); document.body.style.overflow = '' })
</script>

<template>
  <Teleport to="body">
    <div class="backdrop" @mousedown.self="emit('close')">
      <div class="modal" role="dialog" aria-modal="true" :aria-label="title" :style="{ width }">
        <header>
          <h2>{{ title }}</h2>
          <button class="x" type="button" aria-label="بستن" @click="emit('close')">✕</button>
        </header>
        <div class="body"><slot /></div>
        <footer v-if="$slots.footer"><slot name="footer" /></footer>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.backdrop { position: fixed; inset: 0; z-index: 100; background: rgb(0 0 0 / 70%); display: grid; place-items: center; padding: 16px; overflow-y: auto; }
.modal { max-width: 100%; background: var(--surface); border: 1px solid var(--border); border-radius: 14px; margin: auto; }
header { display: flex; align-items: center; justify-content: space-between; padding: 16px 20px; border-bottom: 1px solid var(--border); }
header h2 { margin: 0; font-size: 1.05rem; }
.x { background: none; border: 0; color: var(--muted); cursor: pointer; width: 30px; height: 30px; border-radius: 8px; font-size: 1rem; }
.x:hover { background: var(--surface-2); color: var(--text); }
.body { padding: 20px; display: grid; gap: 14px; }
footer { display: flex; gap: 10px; justify-content: flex-start; padding: 14px 20px; border-top: 1px solid var(--border); background: var(--surface-2); border-radius: 0 0 14px 14px; }

/* موبایل: برگهٔ پایین‌صفحه با لبهٔ امن، همیشه هم‌عرض صفحه */
@media (max-width: 720px) {
  .backdrop { place-items: end stretch; padding: 0; }
  .modal { width: 100% !important; max-height: calc(100dvh - var(--safe-top) - 24px); margin: 0; border-radius: 20px 20px 0 0; border-bottom: 0; display: flex; flex-direction: column; animation: sheet .22s ease-out; }
  .modal::before { content: ''; align-self: center; width: 38px; height: 4px; border-radius: 4px; background: #2c3547; margin-top: 8px; flex: none; }
  header { padding: 10px 18px 12px; }
  .body { padding: 16px 18px; overflow-y: auto; min-height: 0; flex: 1; }
  footer { padding: 12px 18px calc(14px + var(--safe-bottom)); border-radius: 0; }
  footer :deep(.btn) { flex: 1; justify-content: center; }
}
@keyframes sheet { from { transform: translateY(40px); opacity: .4; } }
</style>
