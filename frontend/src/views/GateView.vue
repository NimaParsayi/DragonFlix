<script setup>
import Brand from '../components/Brand.vue'
import Icon from '../components/Icon.vue'
import { auth } from '../lib/auth'
import { botLink } from '../lib/tg'
</script>

<template>
  <div class="gate card">
    <img src="/logo-256.jpg" width="120" height="120" alt="دراگون فلیکس" />
    <Brand size="lg" />
    <template v-if="auth.error">
      <p class="alert error">{{ auth.error }}</p>
      <p class="muted">برنامه رو دوباره از داخل تلگرام باز کن.</p>
    </template>
    <p v-else class="muted">دراگون فلیکس یک «مینی‌اپ» تلگرام است و ورود فقط با حساب تلگرام انجام می‌شه. برای رزرو بلیت، از داخل تلگرام بازش کن.</p>
    <a class="btn lg" :href="botLink" target="_blank" rel="noopener"><Icon name="external" :size="18" /> باز کردن در تلگرام</a>
  </div>
</template>

<style scoped>
.gate { max-width: 420px; margin: 40px auto; display: grid; justify-items: center; gap: 12px; text-align: center; padding: 28px 22px; }
.gate img { border-radius: 50%; }
.gate h1 { margin: 0; }
.gate p { margin: 0; }
</style>
