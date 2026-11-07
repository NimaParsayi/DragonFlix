using System.Security.Cryptography;
using System.Text.Json;
using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>
/// دریافت آپدیت‌های ربات با long polling (بدون نیاز به آدرس عمومی برای وب‌هوک).
/// هر آپدیت در یک scope جدا پردازش می‌شود و خطای یک آپدیت جلوی بقیه را نمی‌گیرد.
/// </summary>
public class TelegramBotService(TelegramApi tg, IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptionsMonitor<TelegramOptions> opts, ILogger<TelegramBotService> log) : BackgroundService
{
    private static readonly string[] Allowed = ["message", "callback_query", "my_chat_member"];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!tg.Configured) { log.LogWarning("Telegram:BotToken تنظیم نشده؛ ربات غیرفعال است."); return; }
        await Task.Delay(1500, ct);

        try
        {
            var me = await tg.GetMeAsync();
            log.LogInformation("ربات تلگرام فعال شد: @{User}", tg.Username);
            await tg.CallAsync("deleteWebhook", new { DropPendingUpdates = false }, ct);
            if (opts.CurrentValue.UseCustomEmoji) await tg.Emoji.LoadAsync(tg, opts.CurrentValue.EmojiPacks, log);
            else tg.Emoji.Enabled = false;
            using var scope = scopes.CreateScope();
            await EnsureAdminInviteAsync(scope.ServiceProvider);
            await scope.ServiceProvider.GetRequiredService<BotSetup>().ApplyAllAsync();
        }
        catch (Exception e) when (e is TelegramApiException or HttpRequestException)
        {
            log.LogError("راه‌اندازی ربات ناموفق بود: {Msg}", e.Message);
        }

        long offset = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var updates = await tg.CallAsync("getUpdates", new { Offset = offset, Timeout = 25, AllowedUpdates = Allowed }, ct);
                foreach (var u in updates.EnumerateArray())
                {
                    offset = u.GetProperty("update_id").GetInt64() + 1;
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<BotUpdateHandler>().HandleAsync(u);
                    }
                    catch (Exception e) { log.LogError(e, "خطا در پردازش آپدیت {Id}", offset - 1); }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (TelegramApiException e) when (e.Code == 429) { await Task.Delay(TimeSpan.FromSeconds(Math.Max(e.RetryAfter, 1)), ct); }
            catch (Exception e)
            {
                log.LogWarning("getUpdates: {Msg}", e.Message);
                try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>
    /// اگر هنوز هیچ ادمینِ تلگرامی نداریم، یک کد راه‌اندازی یک‌بارمصرف می‌سازیم؛ لینک
    /// t.me/Bot?start=admin_کد اولین نفری که بازش کند را ادمین می‌کند (شناسهٔ تلگرام نیاز نیست).
    /// </summary>
    private async Task EnsureAdminInviteAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var settings = sp.GetRequiredService<AppSettings>();
        if (await db.Users.AnyAsync(u => u.Role == Roles.Admin && u.TelegramId != null)) return;
        var code = await settings.GetAsync(AppSettings.AdminSetupCode);
        if (code == "")
        {
            code = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            await settings.SetAsync(AppSettings.AdminSetupCode, code);
        }
        log.LogWarning("لینک ادمین شدن: https://t.me/{User}?start=admin_{Code}", tg.Username, code);
    }
}
