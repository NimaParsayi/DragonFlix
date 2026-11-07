using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonFlix.Api.Services;

/// <summary>تنظیمات قابل ویرایش از پنل ادمین (کلید-مقدار در دیتابیس).</summary>
public class AppSettings(AppDbContext db, IOptionsMonitor<TelegramOptions> tg)
{
    public const string CardNumber = "card.number";
    public const string CardHolder = "card.holder";
    public const string CardBank = "card.bank";
    public const string PayMinutes = "pay.minutes";
    public const string PayNote = "pay.note";
    public const string WebAppUrl = "webapp.url";
    public const string AdminSetupCode = "admin.setup";

    public async Task<string> GetAsync(string key, string fallback = "")
    {
        var s = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        return s?.Value ?? fallback;
    }

    public async Task SetAsync(string key, string? value)
    {
        var s = await db.AppSettings.FindAsync(key);
        if (string.IsNullOrEmpty(value)) { if (s is not null) db.AppSettings.Remove(s); }
        else if (s is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else s.Value = value;
        await db.SaveChangesAsync();
    }

    public record PaymentInfo(string CardNumber, string CardHolder, string CardBank, int Minutes, string Note)
    {
        public bool Configured => CardNumber.Length >= 16;
    }

    public async Task<PaymentInfo> GetPaymentAsync()
    {
        var all = await db.AppSettings.AsNoTracking().Where(x => x.Key.StartsWith("card.") || x.Key.StartsWith("pay.")).ToDictionaryAsync(x => x.Key, x => x.Value);
        string V(string k, string d = "") => all.GetValueOrDefault(k, d);
        var minutes = int.TryParse(V(PayMinutes), out var m) && m is >= 5 and <= 240 ? m : 20;
        return new PaymentInfo(new string(V(CardNumber).Where(char.IsDigit).ToArray()), V(CardHolder), V(CardBank), minutes, V(PayNote));
    }

    /// <summary>آدرس HTTPS مینی‌اپ: تنظیم ادمین بر پیکربندی اولویت دارد. بدون اسلش پایانی.</summary>
    public async Task<string> GetWebAppUrlAsync()
    {
        var v = await GetAsync(WebAppUrl, tg.CurrentValue.WebAppUrl);
        return v.Trim().TrimEnd('/');
    }
}
