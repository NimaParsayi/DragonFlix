import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// در حالت توسعه، درخواست‌های /api به بک‌اند ASP.NET هدایت می‌شود.
export default defineConfig({
  plugins: [vue()],
  server: {
    // آدرس‌های تونل (ngrok و …) برای تست مینی‌اپ داخل تلگرام مجازند
    allowedHosts: ['.ngrok-free.app', '.ngrok-free.dev', '.ngrok.app', '.ngrok.io', '.trycloudflare.com', '.loca.lt'],
    host: true, // روی همهٔ کارت‌های شبکه گوش بده تا از گوشی هم باز شود
    port: 5173,
    proxy: { '/api': process.env.API_TARGET ?? 'http://localhost:5080', '/uploads': process.env.API_TARGET ?? 'http://localhost:5080' },
  },
})
