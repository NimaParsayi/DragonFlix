import { createRouter, createWebHistory } from 'vue-router'
import { auth } from './lib/auth'
import { nav } from './lib/nav'

const routes = [
  { path: '/', name: 'home', component: () => import('./views/HomeView.vue'), meta: { root: true } },
  { path: '/movies/:id', name: 'movie', component: () => import('./views/MovieView.vue'), props: true },
  { path: '/showtimes/:id', name: 'booking', component: () => import('./views/BookingView.vue'), props: true, meta: { flow: true } },
  { path: '/register', name: 'register', component: () => import('./views/RegisterView.vue') },
  { path: '/gate', name: 'gate', component: () => import('./views/GateView.vue') },
  { path: '/login', redirect: { name: 'gate' } },
  { path: '/pay/:id', name: 'pay', component: () => import('./views/PayView.vue'), props: true, meta: { flow: true } },
  { path: '/me', name: 'me', component: () => import('./views/AccountView.vue'), meta: { root: true } },
  { path: '/my-bookings', name: 'my-bookings', component: () => import('./views/MyBookingsView.vue'), meta: { root: true } },
  {
    path: '/admin',
    component: () => import('./views/admin/AdminLayout.vue'),
    meta: { admin: true },
    children: [
      { path: '', name: 'admin-home', component: () => import('./views/admin/AdminHome.vue'), meta: { root: true, admin: true } },
      { path: 'payments', name: 'admin-payments', component: () => import('./views/admin/AdminPayments.vue') },
      { path: 'settings', name: 'admin-settings', component: () => import('./views/admin/AdminSettings.vue') },
      { path: 'backup', name: 'admin-backup', component: () => import('./views/admin/AdminBackup.vue') },
      { path: 'broadcast', name: 'admin-broadcast', component: () => import('./views/admin/AdminBroadcast.vue') },
      { path: 'cinemas', name: 'admin-cinemas', component: () => import('./views/admin/AdminCinemas.vue') },
      { path: 'halls/:id/layout', name: 'admin-layout', component: () => import('./views/admin/HallLayoutDesigner.vue'), props: true },
      { path: 'movies', name: 'admin-movies', component: () => import('./views/admin/AdminMovies.vue') },
      { path: 'sales', name: 'admin-sales', component: () => import('./views/admin/AdminSales.vue') },
      { path: 'tickets', name: 'admin-tickets', component: () => import('./views/admin/AdminTickets.vue') },
      { path: 'slides', name: 'admin-slides', component: () => import('./views/admin/AdminSlides.vue') },
      { path: 'showtimes', name: 'admin-showtimes', component: () => import('./views/admin/AdminShowtimes.vue') },
    ],
  },
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

// ورود فقط با تلگرام است: تا نشست برقرار نشود به صفحهٔ «باز کردن در تلگرام» می‌رویم
router.beforeEach(async (to) => {
  nav.loading = true
  nav.to = to
  await auth.whenReady()
  if (!auth.token) return to.name === 'gate' ? true : { name: 'gate' }
  if (to.name === 'gate') return { name: 'home' }
  // بدون ثبت نام و نام خانوادگی اجازهٔ رزرو نیست
  if (to.meta.flow && !auth.user?.registered) return { name: 'register', query: { next: to.fullPath } }
  if (to.meta.admin && !auth.isAdmin) return { name: 'home' }
})

router.afterEach(() => { nav.loading = false; nav.to = null })
router.onError(() => { nav.loading = false; nav.to = null })

export default router
