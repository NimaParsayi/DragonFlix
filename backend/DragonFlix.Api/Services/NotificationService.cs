using System.Text.Json;
using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services.Telegram;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services;

/// <summary>
/// صف‌کردن اعلان‌ها. اینجا فقط در دیتابیس ذخیره می‌شود؛ ارسال واقعی را NotificationDispatcher با نرخ مجاز انجام می‌دهد.
/// اعلان‌های دلخواه فقط برای کسانی صف می‌شوند که آن دسته را روشن کرده‌اند.
/// </summary>
public class NotificationService(AppDbContext db, AppSettings settings)
{
    public async Task<bool> QueueAsync(User user, string category, string text, object? buttons = null, string media = "",
        int? bookingId = null, bool effect = false, string? dedupeKey = null, DateTime? sendAfter = null)
    {
        if (user.TelegramId is null || user.BotBlocked) return false;
        if (!NotificationCategories.Allows(user, category)) return false;
        if (dedupeKey is not null && await db.Notifications.AnyAsync(n => n.DedupeKey == dedupeKey)) return false;

        db.Notifications.Add(new Notification
        {
            UserId = user.Id, Category = category, Text = text, ButtonsJson = buttons is null ? "" : Kb.ToJson(buttons),
            Media = media, BookingId = bookingId, Effect = effect, DedupeKey = dedupeKey,
            CreatedAt = DateTime.UtcNow, SendAfter = sendAfter ?? DateTime.UtcNow,
        });
        return true;
    }

    public async Task<bool> EnqueueAsync(User user, string category, string text, object? buttons = null, string media = "",
        int? bookingId = null, bool effect = false, string? dedupeKey = null, DateTime? sendAfter = null)
    {
        var added = await QueueAsync(user, category, text, buttons, media, bookingId, effect, dedupeKey, sendAfter);
        if (added) await db.SaveChangesAsync();
        return added;
    }

    /// <summary>به همهٔ ادمین‌هایی که تلگرام دارند (همیشه، مستقل از تنظیمات).</summary>
    public async Task NotifyAdminsAsync(string text, object? buttons = null, string media = "", int? bookingId = null, string? dedupeKey = null)
    {
        var admins = await db.Users.Where(u => u.Role == Roles.Admin && u.TelegramId != null).ToListAsync();
        foreach (var a in admins)
            await QueueAsync(a, "admin", text, buttons, media, bookingId, dedupeKey: dedupeKey is null ? null : $"{dedupeKey}:{a.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>صف‌کردن یک پیام برای چند کاربر (با رعایت تنظیمات هر نفر). تعداد واقعاً صف‌شده را برمی‌گرداند.</summary>
    public async Task<int> BroadcastAsync(IEnumerable<User> users, string category, string text, object? buttons = null, string? dedupePrefix = null)
    {
        var n = 0;
        foreach (var u in users)
            if (await QueueAsync(u, category, text, buttons, dedupeKey: dedupePrefix is null ? null : $"{dedupePrefix}:{u.Id}")) n++;
        await db.SaveChangesAsync();
        return n;
    }

    /// <summary>آدرس مطلق داخل مینی‌اپ؛ اگر آدرس HTTPS هنوز تنظیم نشده null.</summary>
    public async Task<string?> AppUrlAsync(string path = "")
    {
        var b = await settings.GetWebAppUrlAsync();
        return b.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? b + path : null;
    }

    /// <summary>دکمهٔ باز کردن مینی‌اپ روی یک مسیر؛ بدون آدرس HTTPS null برمی‌گرداند.</summary>
    public async Task<object?> WebButtonAsync(string text, string path, string? style = null)
    {
        var url = await AppUrlAsync(path);
        return url is null ? null : Kb.Web(text, url, style);
    }
}
