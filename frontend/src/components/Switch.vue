<script setup>
defineProps({ modelValue: Boolean, disabled: Boolean, label: String })
defineEmits(['update:modelValue'])
</script>

<template>
  <label class="switch" :class="{ disabled }">
    <input type="checkbox" :checked="modelValue" :disabled="disabled" :aria-label="label" @change="$emit('update:modelValue', $event.target.checked)" />
    <span class="knob" />
  </label>
</template>

<style scoped>
.switch { position: relative; display: inline-flex; flex: none; cursor: pointer; }
.switch.disabled { cursor: not-allowed; opacity: .55; }
.switch input { position: absolute; width: 1px; height: 1px; margin: 0; padding: 0; opacity: 0; pointer-events: none; }
.knob { position: relative; width: 44px; height: 26px; border-radius: 999px; background: var(--surface-2); border: 1px solid var(--border); transition: background .18s, border-color .18s; }
.knob::after { content: ''; position: absolute; top: 3px; inset-inline-start: 3px; width: 18px; height: 18px; border-radius: 50%; background: var(--muted); transition: inset-inline-start .18s, background .18s; }
input:checked + .knob { background: var(--primary); border-color: var(--primary); }
input:checked + .knob::after { inset-inline-start: 21px; background: #fff; }
input:focus-visible + .knob { outline: 2px solid var(--sky); outline-offset: 2px; }
</style>
