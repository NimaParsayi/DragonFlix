# ---- build ----
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- serve: Caddy فایل‌های سایت را می‌دهد، /api و /uploads را به بک‌اند می‌فرستد و HTTPS خودکار می‌گیرد ----
FROM caddy:2-alpine
COPY --from=build /app/dist /srv
COPY Caddyfile /etc/caddy/Caddyfile
