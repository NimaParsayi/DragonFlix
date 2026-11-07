<script setup>
import Modal from './Modal.vue'
import Icon from './Icon.vue'
import { toasts, confirmState, answerConfirm } from '../lib/ui'
import { busy } from '../lib/busy'
</script>

<template>
  <Modal v-if="confirmState.open" :title="confirmState.title" width="420px" @close="answerConfirm(false)">
    <p style="margin: 0">{{ confirmState.message }}</p>
    <template #footer>
      <button class="btn" :class="{ 'danger-solid': confirmState.danger }" @click="answerConfirm(true)">{{ confirmState.confirmText }}</button>
      <button class="btn secondary" @click="answerConfirm(false)">انصراف</button>
    </template>
  </Modal>

  <Teleport to="body">
    <Transition name="busy">
      <div v-if="busy.open" class="busy" role="alert" aria-live="assertive" aria-busy="true">
        <div class="bcard">
          <span class="ring" aria-hidden="true" />
          <b>{{ busy.text }}</b>
          <span v-if="busy.sub" class="muted small">{{ busy.sub }}</span>
          <span class="muted small hint">صفحه رو نبند، چند لحظه‌ست</span>
        </div>
      </div>
    </Transition>
  </Teleport>

  <Teleport to="body">
    <div class="toasts" aria-live="polite">
      <div v-for="t in toasts" :key="t.id" class="toast" :class="t.type">
        <Icon :name="t.type === 'error' ? 'alert' : 'check'" :size="18" />
        <span>{{ t.message }}</span>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.toasts { position: fixed; z-index: 200; bottom: 20px; inset-inline-start: 20px; display: grid; gap: 8px; max-width: min(420px, calc(100vw - 40px)); }
.toast { display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-radius: 10px; background: #0f2619; color: #86efac; border: 1px solid #1d4a2e; font-size: .92rem; box-shadow: 0 8px 24px rgb(0 0 0 / 50%); }
.toast.error { background: #2a1417; color: #fca5a5; border-color: #4a2428; }
@media (max-width: 720px) { .toasts { bottom: calc(92px + var(--safe-bottom)); inset-inline: 12px; max-width: none; } }
.btn.danger-solid { background: #dc2626; border-color: #dc2626; }
.btn.danger-solid:hover { background: #b91c1c !important; border-color: #b91c1c !important; }
.busy { position: fixed; inset: 0; z-index: 400; display: grid; place-items: center; padding: 24px; background: rgb(6 8 12 / 72%); backdrop-filter: blur(5px); cursor: progress; }
.bcard { display: grid; justify-items: center; gap: 10px; text-align: center; min-width: min(280px, 100%); padding: 26px 28px; border-radius: 18px; background: var(--surface); border: 1px solid var(--border); box-shadow: 0 20px 60px rgb(0 0 0 / 60%); }
.ring { width: 38px; height: 38px; border-radius: 50%; border: 3px solid #1f3a68; border-top-color: var(--primary); animation: spin .8s linear infinite; }
.hint { opacity: .7; margin-top: 4px; }
@keyframes spin { to { transform: rotate(360deg); } }
.busy-enter-active, .busy-leave-active { transition: opacity .15s; }
.busy-enter-from, .busy-leave-to { opacity: 0; }
@media (prefers-reduced-motion: reduce) { .ring { animation-duration: 2s; } }
</style>
