# راهنمای انتشار روی سرور

معماری: **دو کانتینر** پشت یک دامنه با HTTPS خودکار.

```
کاربر/تلگرام ──HTTPS──▶ web (Caddy) ──┬─ /            ← فایل‌های سایت (Vue)
                                      ├─ /api/*   ─▶ api (ASP.NET :8080)
                                      └─ /uploads/* ─▶ api
```

| کانتینر | وظیفه | داده ماندگار (volume) |
|---|---|---|
| `api` | بک‌اند + ربات تلگرام + پشتیبان شبانه | `df_data` (دیتابیس)، `df_appdata` (رسیدها)، `df_uploads` (عکس فیلم‌ها) |
| `web` | سایت + پروکسی + گواهی HTTPS | `caddy_data` (گواهی‌ها؛ حذفش نکنید) |

---

## ۰. پیش‌نیازها

1. **سرور لینوکس** (Ubuntu 22.04+ کافی است؛ ۱ گیگ رم کم‌ترین حد، ۲ گیگ بهتر).
2. **دامنه** با رکورد `A` به IP سرور (مثلاً `flix.example.com`). تلگرام فقط HTTPS را برای مینی‌اپ قبول می‌کند.
3. پورت‌های **۸۰ و ۴۴۳** روی سرور باز باشند (برای گرفتن گواهی هم ۸۰ لازم است).
4. سرور باید به `api.telegram.org` دسترسی داشته باشد. اگر ندارد، آدرس یک پروکسی/آینه را در `TELEGRAM_API_BASE` بگذار.
5. توکن ربات از BotFather (و اگر قبلاً توکن را جایی نشان داده‌ای، با `/revoke` عوضش کن).

## ۱. آماده‌سازی سرور (یک‌بار)

```bash
# نصب Docker (شامل compose)
curl -fsSL https://get.docker.com | sh

# پوشهٔ برنامه
sudo mkdir -p /opt/dragonflix && sudo chown $USER /opt/dragonflix
cd /opt/dragonflix
```

فایل `.env` را بساز (نمونه: [`.env.example`](.env.example)):

```bash
nano .env      # DOMAIN, BOT_TOKEN, JWT_KEY, TMDB_API_KEY, GHCR_OWNER را پر کن
```

- `JWT_KEY`: با `openssl rand -base64 48` بساز. اگر مقدار پیش‌فرض بماند API بالا نمی‌آید.
- `GHCR_OWNER`: نام کاربری یا سازمان گیت‌هاب‌ات (**حروف کوچک**).

## ۲. اتصال گیت‌هاب (یک‌بار)

۱) پروژه را در یک ریپوی گیت‌هاب بگذار (فایل‌های `.env` و `appsettings.Local.json` در `.gitignore` هستند و نباید commit شوند).

۲) یک کلید SSH مخصوص دیپلوی بساز و بخش عمومی‌اش را روی سرور بگذار:

```bash
ssh-keygen -t ed25519 -f deploy_key -N "" -C "github-deploy"
ssh-copy-id -i deploy_key.pub USER@SERVER_IP      # یا محتوای deploy_key.pub را در ~/.ssh/authorized_keys سرور بگذار
```

۳) در گیت‌هاب: **Settings ← Secrets and variables ← Actions ← New repository secret**

| Secret | مقدار |
|---|---|
| `SSH_HOST` | IP یا دامنهٔ سرور |
| `SSH_USER` | کاربر SSH (مثلاً `ubuntu` یا `root`) |
| `SSH_KEY` | **محتوای کامل** فایل `deploy_key` (کلید خصوصی) |
| `SSH_PORT` | اختیاری، پیش‌فرض ۲۲ |
| `DEPLOY_PATH` | اختیاری، پیش‌فرض `/opt/dragonflix` |

۴) **Settings ← Actions ← General ← Workflow permissions** را روی «Read and write permissions» بگذار (برای فرستادن ایمیج به GHCR).

## ۳. چطور کار می‌کند (CI/CD)

- **`.github/workflows/ci.yml`**: روی هر PR و هر branch غیر از `main` — بیلد بک‌اند، بیلد فرانت و بیلد آزمایشی Dockerfileها.
- **`.github/workflows/deploy.yml`**: با هر push روی `main`:
  1. ایمیج `dragonflix-api` و `dragonflix-web` ساخته و به `ghcr.io/<owner>/` فرستاده می‌شود (تگ `latest` و تگ commit).
  2. `docker-compose.yml` روی سرور کپی می‌شود.
  3. سرور با SSH آپدیت می‌شود: `docker compose pull` ← `up -d` (توقف چند ثانیه‌ای).
  4. تگ در حال اجرا در `.env` ثبت می‌شود.

اولین push روی `main` همه‌چیز را بالا می‌آورد. دیتابیس خودکار ساخته می‌شود.

> اگر پکیج‌های GHCR خصوصی‌اند مشکلی نیست؛ دیپلوی با توکن موقت همان job لاگین می‌کند. برای pull دستی روی سرور باید یک بار `docker login ghcr.io` با یک Personal Access Token (دسترسی `read:packages`) بزنی.

## ۴. بعد از اولین دیپلوی

1. `https://DOMAIN/api/health` باید `{"ok":true}` بدهد.
2. **ادمین شدن**: لینک راه‌اندازی در لاگ است؛ اولین نفری که بازش کند ادمین می‌شود:
   ```bash
   cd /opt/dragonflix && docker compose logs api | grep "admin_"
   ```
3. در ربات `/start` بزن، دکمهٔ «باز کردن دراگون‌فلیکس» باید مینی‌اپ را باز کند.
4. از پنل: **پرداخت و ربات** ← شمارهٔ کارت را ثبت کن؛ بعد سینما، سالن، فیلم و سانس را بساز.
5. **پشتیبان‌گیری** ← بخش پنل را ببین؛ هر شب ساعت ۱۲ (به وقت `TZ`) برای همهٔ ادمین‌ها در تلگرام می‌آید.

## ۵. کارهای روزمره

```bash
cd /opt/dragonflix
docker compose ps                    # وضعیت
docker compose logs -f api           # لاگ زندهٔ بک‌اند
docker compose logs -f web           # لاگ وب/HTTPS
docker compose restart api           # ری‌استارت
```

**Rollback به نسخهٔ قبل**: در `.env` مقدار `TAG` را روی SHA commit قبلی بگذار و:
```bash
docker compose pull && docker compose up -d
```

**آپدیت دستی بدون گیت‌هاب**: `docker compose pull && docker compose up -d`
(یا روی خود سرور با کد: `docker compose up -d --build`).

## ۶. پشتیبان و بازگردانی

پشتیبان تلگرامی شامل `dragonflix.db`، پوشهٔ `receipts/` و `uploads/` است (اگر بزرگ باشد چند بخش می‌شود؛ با `cat name.zip.* > name.zip` یکی کن).

بازگردانی روی سرور جدید:

```bash
cd /opt/dragonflix
unzip dragonflix-backup-*.zip -d restore
docker compose up -d --no-deps api && docker compose stop api   # فقط برای ساخت volumeها
docker run --rm -v dragonflix_df_data:/d -v "$PWD/restore:/r" alpine sh -c "cp /r/dragonflix.db /d/ && rm -f /d/dragonflix.db-wal /d/dragonflix.db-shm && chown 1654:1654 /d/dragonflix.db"
docker run --rm -v dragonflix_df_appdata:/d -v "$PWD/restore:/r" alpine sh -c "cp -r /r/receipts/. /d/receipts/ 2>/dev/null; chown -R 1654:1654 /d"
docker run --rm -v dragonflix_df_uploads:/d -v "$PWD/restore:/r" alpine sh -c "cp -r /r/uploads/. /d/ 2>/dev/null; chown -R 1654:1654 /d"
docker compose up -d
```

(نام volumeها با نام پوشه پیشوند می‌گیرد؛ اگر پوشه `dragonflix` نیست، با `docker volume ls` نام دقیق را ببین.)

## ۷. چک‌لیست امنیتی

- [ ] `JWT_KEY` تصادفی و بلند است.
- [ ] توکن ربات فقط در `.env` سرور است (نه در گیت).
- [ ] کلید TMDB از `appsettings.json` برداشته شده و فقط در `.env`/`appsettings.Local.json` است (اگر قبلاً در گیت رفته، در TMDB عوضش کن).
- [ ] فقط پورت‌های ۸۰/۴۴۳ (و SSH) باز هستند؛ پورت ۸۰۸۰ بیرون منتشر نمی‌شود.
- [ ] ورود SSH با رمز بسته و فقط با کلید باشد.
