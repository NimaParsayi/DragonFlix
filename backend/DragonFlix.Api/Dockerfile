# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DragonFlix.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# libfontconfig1: لازم برای ساخت PDF/تصویر بلیت (QuestPDF/Skia)؛ curl: برای healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
# داده‌های ماندگار (با volume نصب می‌شوند): دیتابیس، رسیدها، عکس فیلم‌ها
RUN mkdir -p /data /app/App_Data /app/wwwroot/uploads/movies \
    && chown -R app:app /data /app/App_Data /app/wwwroot/uploads
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="Data Source=/data/dragonflix.db"
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "DragonFlix.Api.dll"]
