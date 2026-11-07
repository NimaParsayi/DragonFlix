import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { auth } from './lib/auth'
import * as tg from './lib/tg'
import './style.css'

tg.setup()
createApp(App).use(router).mount('#app')
auth.bootstrap()

// دکمهٔ بازگشت بومی تلگرام: روی صفحه‌های داخلی نشان داده می‌شود
router.afterEach((to) => {
  if (to.meta.root || to.name === 'gate') { tg.backButton.hide(); return }
  tg.backButton.show(() => (window.history.state?.back ? router.back() : router.replace({ name: to.meta.admin ? 'admin-home' : 'home' })))
})

// بعد از اولین نمایش، صفحه‌های اصلی در پس‌زمینه دانلود می‌شوند تا جابه‌جایی بین تب‌ها فوری باشد
const idle = window.requestIdleCallback || ((f) => setTimeout(f, 800))
idle(() => {
  import('./views/MyBookingsView.vue'); import('./views/MovieView.vue'); import('./views/BookingView.vue')
  import('./views/PayView.vue'); import('./views/AccountView.vue')
})
