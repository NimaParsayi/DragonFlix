import { auth } from './auth'

const BASE = import.meta.env.VITE_API_URL ?? ''

export class ApiError extends Error {
  constructor(message, status) {
    super(message)
    this.status = status
  }
}

async function request(method, url, body, retried = false) {
  const headers = {}
  const isForm = body instanceof FormData // آپلود فایل؛ مرورگر خودش Content-Type را می‌گذارد
  if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  if (auth.token) headers.Authorization = `Bearer ${auth.token}`

  let res
  try {
    res = await fetch(BASE + url, { method, headers, body: body === undefined ? undefined : isForm ? body : JSON.stringify(body) })
  } catch {
    throw new ApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (res.status === 401 && auth.token && !retried) {
    // توکن منقضی شده؛ یک‌بار با initData دوباره وارد می‌شویم و درخواست را تکرار می‌کنیم
    if (await auth.bootstrap()) return request(method, url, body, true)
  }
  if (res.status === 204) return null

  const data = await res.json().catch(() => null)
  if (!res.ok) {
    const firstValidation = data?.errors && Object.values(data.errors)[0]?.[0]
    throw new ApiError(data?.message || firstValidation || 'خطایی رخ داد.', res.status)
  }
  return data
}

// دانلود فایل (مثلاً PDF) با توکن کاربر
export async function downloadFile(url, fallbackName) {
  let res
  try {
    res = await fetch(BASE + url, { headers: auth.token ? { Authorization: `Bearer ${auth.token}` } : {} })
  } catch {
    throw new ApiError('ارتباط با سرور برقرار نشد.', 0)
  }
  if (!res.ok) throw new ApiError('دریافت فایل ممکن نشد.', res.status)
  const name = /filename=([^;]+)/.exec(res.headers.get('Content-Disposition') ?? '')?.[1]?.replace(/"/g, '') ?? fallbackName
  const href = URL.createObjectURL(await res.blob())
  const a = Object.assign(document.createElement('a'), { href, download: name })
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(href), 10_000)
}

// عکس‌های خصوصی (مثل رسید) با <img> بدون توکن بارگذاری نمی‌شوند؛ با توکن دریافت و به آدرس موقت تبدیل می‌شوند
export async function fetchObjectUrl(url) {
  const res = await fetch(BASE + url, { headers: auth.token ? { Authorization: `Bearer ${auth.token}` } : {} })
  if (!res.ok) throw new ApiError('تصویر در دسترس نیست.', res.status)
  return URL.createObjectURL(await res.blob())
}

export const api = {
  get: (u) => request('GET', u),
  post: (u, b) => request('POST', u, b ?? {}),
  put: (u, b) => request('PUT', u, b),
  del: (u) => request('DELETE', u),
  upload: (u, formData) => request('POST', u, formData),
}
