using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using DragonFlix.Api.Services.Telegram;
using DragonFlix.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>تنظیمات پرداخت (کارت مقصد، مهلت) و وضعیت/آدرس ربات تلگرام.</summary>
[ApiController, Route("api/admin/settings"), Authorize(Roles = Roles.Admin)]
public class AdminSettingsController(AppSettings settings, AppDbContext db, TelegramApi tg, BotSetup bot) : ControllerBase
{
    [HttpGet("payment")]
    public async Task<object> GetPayment()
    {
        var p = await settings.GetPaymentAsync();
        return new { p.CardNumber, p.CardHolder, p.CardBank, detectedBank = BankInfo.NameOf(p.CardNumber), bankLogo = BankInfo.LogoUrl(p.CardNumber), p.Minutes, p.Note, p.Configured };
    }

    [HttpGet("card-image")]
    public async Task<IActionResult> CardImagePreview()
    {
        var p = await settings.GetPaymentAsync();
        var png = CardImage.Render(p.CardNumber, p.CardHolder, p.CardBank);
        return png is null ? NotFound() : File(png, "image/png");
    }

    [HttpPut("payment")]
    public async Task<IActionResult> PutPayment(PaymentSettingsDto dto)
    {
        var digits = new string((dto.CardNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits != "" && digits.Length != 16) return BadRequest(new { message = "شمارهٔ کارت باید ۱۶ رقم باشد." });
        if (dto.Minutes is < 5 or > 240) return BadRequest(new { message = "مهلت پرداخت باید بین ۵ تا ۲۴۰ دقیقه باشد." });
        await settings.SetAsync(AppSettings.CardNumber, digits);
        await settings.SetAsync(AppSettings.CardHolder, dto.CardHolder?.Trim());
        await settings.SetAsync(AppSettings.CardBank, dto.CardBank?.Trim());
        await settings.SetAsync(AppSettings.PayMinutes, dto.Minutes.ToString());
        await settings.SetAsync(AppSettings.PayNote, dto.Note?.Trim());
        return NoContent();
    }

    [HttpGet("telegram")]
    public async Task<object> GetTelegram()
    {
        var url = await settings.GetWebAppUrlAsync();
        var hasAdmin = await db.Users.AnyAsync(u => u.Role == Roles.Admin && u.TelegramId != null);
        return new
        {
            configured = tg.Configured, botUsername = tg.Username,
            botLink = tg.Username is null ? null : $"https://t.me/{tg.Username}",
            webAppUrl = url, webAppHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            adminCount = await db.Users.CountAsync(u => u.Role == Roles.Admin && u.TelegramId != null),
            usersWithTelegram = await db.Users.CountAsync(u => u.TelegramId != null),
            reachable = await db.Users.CountAsync(u => u.TelegramId != null && !u.BotBlocked),
            queued = await db.Notifications.CountAsync(n => n.SentAt == null && n.Attempts < 5),
            hasAdmin,
        };
    }

    /// <summary>آدرس HTTPS مینی‌اپ را ذخیره می‌کند و دکمهٔ منوی ربات را روی آن تنظیم می‌کند.</summary>
    [HttpPut("telegram/webapp")]
    public async Task<IActionResult> PutWebApp(WebAppUrlDto dto)
    {
        var url = (dto.Url ?? "").Trim().TrimEnd('/');
        if (url != "" && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
            return BadRequest(new { message = "آدرس مینی‌اپ باید با https:// شروع شود (تلگرام فقط HTTPS را می‌پذیرد)." });
        await settings.SetAsync(AppSettings.WebAppUrl, url);
        string? warning = null;
        try { await bot.ApplyMenuAsync(); } catch (TelegramApiException e) { warning = e.Message; }
        return Ok(new { warning });
    }

    /// <summary>لینک یک‌بارمصرف برای ادمین‌کردن یک نفر دیگر؛ او باید لینک را در تلگرام باز کند.</summary>
    [HttpPost("telegram/admin-invite")]
    public async Task<IActionResult> AdminInvite()
    {
        if (tg.Username is null) return Conflict(new { message = "ربات هنوز راه‌اندازی نشده است." });
        var code = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        await settings.SetAsync(AppSettings.AdminSetupCode, code);
        return Ok(new { link = $"https://t.me/{tg.Username}?start=admin_{code}" });
    }

    /// <summary>اعمال دوبارهٔ فرمان‌ها، دکمهٔ منو و مشخصات ربات (نام، توضیح).</summary>
    [HttpPost("telegram/apply")]
    public async Task<IActionResult> Apply()
    {
        try { await bot.ApplyAllAsync(force: true); return NoContent(); }
        catch (TelegramApiException e) { return StatusCode(502, new { message = e.Message }); }
    }
}
