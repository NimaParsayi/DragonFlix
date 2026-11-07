using System.Text.Json;
using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>پیکربندی ظاهر ربات: نام، توضیح، لوگو، فرمان‌ها (جدا برای ادمین) و دکمهٔ منوی مینی‌اپ.</summary>
public class BotSetup(TelegramApi tg, AppSettings settings, AppDbContext db, IWebHostEnvironment env, ILogger<BotSetup> log)
{
    public const string BotName = "دراگون‌فلیکس | DragonFlix";
    private const string ShortDescription = "رزرو آنلاین بلیت سینما با انتخاب صندلی، پرداخت کارت‌به‌کارت و بلیت QR 🎟";
    private const string Description =
        "🎬 دراگون‌فلیکس؛ رزرو آنلاین بلیت سینما\n\n" +
        "فیلم و سانس دلخواهت رو انتخاب کن، صندلی‌ات رو روی نقشهٔ سالن بزن، کارت‌به‌کارت پرداخت کن و بلیت QRدار رو همین‌جا تحویل بگیر.\n\n" +
        "🔔 یادآوری سانس، خبر اکران فیلم‌های جدید و سانس‌های تازه — همه داخل تلگرام.\n\n" +
        "برای شروع «Start» رو بزن 👇";

    private static readonly object[] UserCommands =
    [
        new { Command = "start", Description = "شروع و باز کردن دراگون‌فلیکس" },
        new { Command = "movies", Description = "فیلم‌های روی پرده" },
        new { Command = "tickets", Description = "بلیت‌های من" },
        new { Command = "settings", Description = "انتخاب اعلان‌هایی که می‌خواهم" },
        new { Command = "help", Description = "راهنما" },
    ];

    private static readonly object[] AdminCommands =
    [
        .. UserCommands,
        new { Command = "pending", Description = "رسیدهای در انتظار تأیید" },
        new { Command = "stats", Description = "آمار امروز" },
        new { Command = "backup", Description = "دریافت پشتیبان دیتابیس" },
    ];

    public async Task ApplyAllAsync(bool force = false)
    {
        if (!tg.Configured) return;
        await ApplyCommandsAsync();
        await ApplyMenuAsync();
        if (force || await settings.GetAsync("bot.profile.v1") == "")
        {
            await ApplyProfileAsync();
            await settings.SetAsync("bot.profile.v1", "1");
        }
    }

    public async Task ApplyCommandsAsync()
    {
        await tg.CallAsync("setMyCommands", new { Commands = UserCommands, Scope = new { Type = "default" } });
        await tg.CallAsync("setMyCommands", new { Commands = UserCommands, Scope = new { Type = "default" }, LanguageCode = "fa" });
        // ادمین‌ها فرمان‌های بیشتری می‌بینند
        var admins = await db.Users.Where(u => u.Role == Roles.Admin && u.TelegramId != null).Select(u => u.TelegramId!.Value).ToListAsync();
        foreach (var id in admins)
            try { await tg.CallAsync("setMyCommands", new { Commands = AdminCommands, Scope = new { Type = "chat", ChatId = id } }); }
            catch (TelegramApiException e) { log.LogDebug("admin commands for {Id}: {Msg}", id, e.Message); } // هنوز با ربات چت نکرده
    }

    private async Task<object> MenuAsync()
    {
        var url = await settings.GetWebAppUrlAsync();
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? new { Type = "web_app", Text = "🎬 دراگون‌فلیکس", WebApp = new { Url = url } }
            : new { Type = "commands" };
    }

    /// <summary>
    /// دکمهٔ کنار کادر پیام: با آدرس HTTPS معتبر «مینی‌اپ»، وگرنه فهرست فرمان‌ها. علاوه بر پیش‌فرض عمومی، برای هر کاربر
    /// هم جدا تنظیم می‌شود (تلگرام در عمل دکمهٔ هر چت را از تنظیم همان چت می‌خواند).
    /// </summary>
    public async Task ApplyMenuAsync()
    {
        var menu = await MenuAsync();
        await tg.CallAsync("setChatMenuButton", new { MenuButton = menu });
        var ids = await db.Users.Where(u => u.TelegramId != null && !u.BotBlocked).Select(u => u.TelegramId!.Value).Take(2000).ToListAsync();
        foreach (var id in ids)
        {
            try { await tg.CallAsync("setChatMenuButton", new { ChatId = id, MenuButton = menu }); }
            catch (TelegramApiException e) { log.LogDebug("menu for {Id}: {Msg}", id, e.Message); }
            await Task.Delay(40); // زیر سقف نرخ تلگرام
        }
    }

    public async Task ApplyMenuForChatAsync(long chatId) =>
        await tg.CallAsync("setChatMenuButton", new { ChatId = chatId, MenuButton = await MenuAsync() });

    public async Task ApplyProfileAsync()
    {
        foreach (var lang in new string?[] { null, "fa" })
        {
            await tg.CallAsync("setMyName", new { Name = BotName, LanguageCode = lang });
            await tg.CallAsync("setMyDescription", new { Description, LanguageCode = lang });
            await tg.CallAsync("setMyShortDescription", new { ShortDescription, LanguageCode = lang });
        }

        // عکس پروفایل ربات (Bot API 9.4+)؛ اگر نسخهٔ API پشتیبانی نکند بی‌صدا رد می‌شود و باید از BotFather گذاشت
        var logo = Path.Combine(AppContext.BaseDirectory, "Assets", "bot-logo.jpg");
        if (File.Exists(logo))
        {
            try
            {
                await tg.CallMultipartAsync("setMyProfilePhoto",
                    new() { ["photo"] = JsonSerializer.Serialize(new { type = "static", photo = "attach://logo" }) },
                    "logo", "logo.jpg", await File.ReadAllBytesAsync(logo), "image/jpeg");
            }
            catch (TelegramApiException e) { log.LogWarning("setMyProfilePhoto: {Msg}", e.Message); }
        }
    }
}
