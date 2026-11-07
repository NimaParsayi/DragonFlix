using System.Text.Json;
using DragonFlix.Api.Controllers;
using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>
/// ارسال صف اعلان‌ها با نرخ مجاز (حدود ۲۰ پیام در ثانیه؛ سقف تلگرام ۳۰). در صورت 429 صبر می‌کند،
/// کاربرِ مسدودکننده را علامت می‌زند و خطاهای موقت را با فاصله تکرار می‌کند.
/// </summary>
public class NotificationDispatcher(TelegramApi tg, IServiceScopeFactory scopes, IWebHostEnvironment env, ILogger<NotificationDispatcher> log) : BackgroundService
{
    private const string ConfettiEffect = "5046509860389126442"; // افکت 🎉 پیام (چت خصوصی)
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(3000, ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!tg.Configured) { await Task.Delay(10_000, ct); continue; }
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var due = await db.Notifications.Where(n => n.SentAt == null && n.Attempts < MaxAttempts && n.SendAfter <= DateTime.UtcNow)
                    .OrderBy(n => n.Id).Take(20).ToListAsync(ct);
                if (due.Count == 0) { await Task.Delay(2000, ct); continue; }

                foreach (var n in due)
                {
                    var user = await db.Users.FindAsync([n.UserId], ct);
                    if (user?.TelegramId is null || user.BotBlocked) { n.SentAt = DateTime.UtcNow; n.Error = "کاربر در دسترس نیست"; await db.SaveChangesAsync(ct); continue; }
                    try
                    {
                        await SendOneAsync(scope.ServiceProvider, db, n, user.TelegramId.Value, ct);
                        n.SentAt = DateTime.UtcNow; n.Error = "";
                    }
                    catch (TelegramApiException e) when (e.Code == 429)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(e.RetryAfter, 1)), ct);
                        break; // بعد از صبر، همین پیام‌ها دوباره انتخاب می‌شوند
                    }
                    catch (TelegramApiException e) when (e.Unreachable)
                    {
                        user.BotBlocked = true; n.SentAt = DateTime.UtcNow; n.Error = e.Message; // مسدود کرده؛ دوباره تلاش نمی‌کنیم
                    }
                    catch (Exception e)
                    {
                        n.Attempts++; n.Error = e.Message;
                        n.SendAfter = DateTime.UtcNow.AddSeconds(30 * n.Attempts);
                        log.LogWarning("ارسال اعلان {Id} ناموفق ({Attempts}): {Msg}", n.Id, n.Attempts, e.Message);
                    }
                    await db.SaveChangesAsync(ct);
                    await Task.Delay(50, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                log.LogError(e, "خطای dispatcher");
                try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task SendOneAsync(IServiceProvider sp, AppDbContext db, Notification n, long chat, CancellationToken ct)
    {
        JsonElement? markup = string.IsNullOrEmpty(n.ButtonsJson) ? null : JsonDocument.Parse(n.ButtonsJson).RootElement.Clone();
        var effect = n.Effect ? ConfettiEffect : null;

        try { await DeliverAsync(sp, db, n, chat, markup, effect, ct); }
        catch (TelegramApiException e) when (effect is not null && e.Code == 400 && e.Message.Contains("effect", StringComparison.OrdinalIgnoreCase))
        {
            await DeliverAsync(sp, db, n, chat, markup, null, ct); // افکت پشتیبانی نشد؛ بدونش بفرست
        }
    }

    private async Task DeliverAsync(IServiceProvider sp, AppDbContext db, Notification n, long chat, object? markup, string? effect, CancellationToken ct)
    {
        switch (n.Media)
        {
            case "ticket" when n.BookingId is { } id:
            {
                var b = await db.Bookings.Include(x => x.User).Include(x => x.Seats).ThenInclude(s => s.SeatRow)
                    .Include(x => x.Showtime).ThenInclude(s => s!.Movie).Include(x => x.Showtime).ThenInclude(s => s!.Hall).ThenInclude(h => h!.Cinema)
                    .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
                if (b is not null)
                {
                    await SendPhotoWithCaptionAsync(chat, await TicketPng(BookingsController.ToTicket(b)), n.Text, markup, effect, ct);
                    return;
                }
                break;
            }
            case "receipt" when n.BookingId is { } id:
            {
                var file = await db.Bookings.Where(x => x.Id == id).Select(x => x.ReceiptFile).FirstOrDefaultAsync(ct);
                if (!string.IsNullOrEmpty(file) && await sp.GetRequiredService<ReceiptStore>().ReadAsync(file) is { } r)
                {
                    await SendPhotoWithCaptionAsync(chat, r.Bytes, n.Text, markup, null, ct);
                    return;
                }
                break;
            }
            case "card":
            {
                var pay = await sp.GetRequiredService<AppSettings>().GetPaymentAsync();
                if (CardImage.Render(pay.CardNumber, pay.CardHolder, pay.CardBank) is { } png)
                {
                    await SendPhotoWithCaptionAsync(chat, png, n.Text, markup, null, ct);
                    return;
                }
                break;
            }
            case "poster" when n.BookingId is { } mid: // BookingId در این حالت شناسهٔ فیلم است
            {
                var url = await db.Movies.Where(x => x.Id == mid).Select(x => x.PosterUrl).FirstOrDefaultAsync(ct);
                if (await TrySendPosterAsync(chat, url, n.Text, markup, ct)) return;
                break;
            }
        }
        await tg.SendMessageAsync(chat, n.Text, markup, effect, ct);
    }

    private static async Task<byte[]> TicketPng(TicketData t) => TicketImage.Render(t, await PosterSource.LoadAsync(t.PosterUrl));

    /// <summary>کپشن تلگرام حداکثر ۱۰۲۴ نویسه است؛ بلندتر از آن را جدا می‌فرستیم.</summary>
    private async Task SendPhotoWithCaptionAsync(long chat, byte[] photo, string html, object? markup, string? effect, CancellationToken ct)
    {
        if (html.Length <= 1000) { await tg.SendPhotoAsync(chat, photo, html, markup, effect, ct); return; }
        await tg.SendPhotoAsync(chat, photo, "", null, effect, ct);
        await tg.SendMessageAsync(chat, html, markup, null, ct);
    }

    private async Task<bool> TrySendPosterAsync(long chat, string posterUrl, string html, object? markup, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(posterUrl) || html.Length > 1000) return false;
        try
        {
            if (MovieImages.IsManaged(posterUrl))
            {
                var path = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "movies", Path.GetFileName(posterUrl));
                if (!File.Exists(path)) return false;
                await tg.SendPhotoAsync(chat, await File.ReadAllBytesAsync(path, ct), html, markup, null, ct);
            }
            else if (posterUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                await tg.CallAsync("sendPhoto", new { ChatId = chat, Photo = posterUrl, Caption = html, ParseMode = "HTML", ReplyMarkup = markup }, ct);
            else return false;
            return true;
        }
        catch (TelegramApiException e) when (e.Code == 400) { return false; } // آدرس تصویر قابل‌دریافت نبود؛ پیام متنی می‌رود
    }
}
