using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>
/// کارهای زمان‌بندی‌شده (هر ۳۰ ثانیه): انقضای رزروهای پرداخت‌نشده، هشدار پایان مهلت، یادآوری سانس برای دارندگان بلیت
/// و یادآوری به ادمین برای رسیدهای مانده‌ی بررسی‌نشده. همه idempotent هستند (کلید dedupe).
/// </summary>
public class Scheduler(IServiceScopeFactory scopes, BackupService backup, ILogger<Scheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(5000, ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sp = scope.ServiceProvider;
                var payments = sp.GetRequiredService<PaymentService>();
                var expired = await payments.ExpireOverdueAsync();
                await payments.WarnNearDeadlineAsync();
                await RemindShowtimesAsync(sp);
                await RemindAdminsAboutStaleReceiptsAsync(sp);
                await NightlyBackupAsync(sp);
                if (expired > 0) log.LogInformation("{N} رزرو پرداخت‌نشده منقضی شد.", expired);
            }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "خطای scheduler"); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>هر شب ساعت ۰۰:۰۰ (به وقت سرور) یک‌بار؛ اگر سرور آن لحظه خاموش بود، همان روز در اولین فرصت.</summary>
    private async Task NightlyBackupAsync(IServiceProvider sp)
    {
        var settings = sp.GetRequiredService<AppSettings>();
        if (await settings.GetAsync(BackupService.AutoKey, "1") != "1") return;
        var today = DateTime.Now.ToString("yyyyMMdd");
        if (await settings.GetAsync(BackupService.LastAutoDayKey) == today) return;
        if (!backup.Start("auto")) return;
        await settings.SetAsync(BackupService.LastAutoDayKey, today);
    }

    private static async Task RemindShowtimesAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var notify = sp.GetRequiredService<NotificationService>();
        var now = DateTime.Now;
        var until = now.AddHours(3);
        var ids = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.CheckedInAt == null
                && b.Showtime!.StartsAt > now.AddMinutes(15) && b.Showtime.StartsAt <= until).Select(b => b.Id).ToListAsync();
        foreach (var id in ids)
        {
            var b = await sp.GetRequiredService<PaymentService>().LoadAsync(id);
            if (b is null) continue;
            var rows = new List<object[]>();
            if (await notify.WebButtonAsync("🎟 نمایش بلیت", $"/my-bookings?ticket={b.Id}", "primary") is { } web) rows.Add([web]);
            await notify.EnqueueAsync(b.User!, "reminder", Messages.Reminder(b), rows.Count > 0 ? Kb.Markup(rows.ToArray()) : null, dedupeKey: $"reminder:{id}");
        }
    }

    private static async Task RemindAdminsAboutStaleReceiptsAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.Now.AddMinutes(-30);
        var stale = await db.Bookings.Where(b => b.Status == BookingStatus.AwaitingReview && b.SubmittedAt < cutoff).Select(b => b.Id).ToListAsync();
        if (stale.Count == 0) return;
        var key = $"stale:{DateTime.Now:yyyyMMddHH}"; // حداکثر ساعتی یک‌بار
        var markup = Kb.Markup([[Kb.Cb("📋 دیدن رسیدها", "ls:pending")]]);
        await sp.GetRequiredService<NotificationService>().NotifyAdminsAsync(
            $"⏰ <b>{PersianFormat.Digits(stale.Count.ToString())} تا رسید بیشتر از ۳۰ دقیقه‌ست منتظر بررسی مونده.</b>\nبرای دیدن: /pending", markup, dedupeKey: key);
    }
}
