using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services.Telegram;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services;

/// <summary>اعلان‌های برنامهٔ اکران: فیلم جدید، سانس‌های تازهٔ فیلم‌های دنبال‌شده و تغییر زمان سانسِ رزروشده.</summary>
public class ReleaseNotifier(AppDbContext db, NotificationService notify)
{
    private static string E(string? s) => TelegramApi.Escape(s);

    public async Task OnShowtimeCreatedAsync(int movieId)
    {
        var m = await db.Movies.FindAsync(movieId);
        if (m is null) return;

        var first = await db.Showtimes.Where(s => s.MovieId == movieId && s.StartsAt > DateTime.Now).OrderBy(s => s.StartsAt)
            .Select(s => new { s.StartsAt, Hall = s.Hall!.Name, Cinema = s.Hall.Cinema!.Name }).FirstOrDefaultAsync();
        var buttons = new List<object>();
        if (await notify.WebButtonAsync("🎟 رزرو بلیت", $"/movies/{movieId}", "primary") is { } web) buttons.Add(web);
        buttons.Add(Kb.Cb("🔔 دنبال کردن", $"fw:{movieId}"));
        var markup = Kb.Markup(buttons.ToArray());

        if (m.AnnouncedAt is null)
        {
            // اولین بار که این فیلم سانس می‌گیرد: «اکران فیلم جدید» برای همهٔ علاقه‌مندان
            m.AnnouncedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var genres = string.Join(" · ", m.Genre.Split(',', StringSplitOptions.RemoveEmptyEntries));
            var desc = m.Description.Length > 280 ? m.Description[..280] + "…" : m.Description;
            var text = $"🎬 <b>اکران جدید: {E(m.Title)}</b>\n" +
                       (m.SpecialNote != "" ? $"✨ <b>{E(m.SpecialNote)}</b>\n" : "") +
                       $"{E(genres)} · {PersianFormat.Digits(m.DurationMinutes.ToString())} دقیقه" +
                       (m.AgeRating switch { "all" => " · همه سنین", "" => "", var a => $" · +{PersianFormat.Digits(a)}" }) + "\n" +
                       (desc != "" ? $"\n<blockquote expandable>{E(desc)}</blockquote>\n" : "") +
                       (first is null ? "" : $"\n📅 اولین سانس: {PersianFormat.Date(first.StartsAt)} ⏰ {PersianFormat.Time(first.StartsAt)}\n🏛 {E(first.Cinema)} · {E(first.Hall)}");

            var users = await db.Users.Where(u => u.TelegramId != null && !u.BotBlocked).ToListAsync();
            var n = 0;
            foreach (var u in users)
                if (await notify.QueueAsync(u, "new_movie", text, markup, media: m.PosterUrl != "" ? "poster" : "", bookingId: movieId, dedupeKey: $"newmovie:{movieId}:{u.Id}")) n++;
            await db.SaveChangesAsync();
            return;
        }

        // فیلمِ قبلاً اعلام‌شده: سانس تازه فقط برای دنبال‌کنندگان و کسانی که قبلاً بلیت همین فیلم را گرفته‌اند
        var followers = db.MovieFollows.Where(f => f.MovieId == movieId).Select(f => f.UserId);
        var bookers = db.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.Showtime!.MovieId == movieId).Select(b => b.UserId);
        var targets = await db.Users.Where(u => u.TelegramId != null && !u.BotBlocked && (followers.Contains(u.Id) || bookers.Contains(u.Id))).ToListAsync();
        var txt = $"🆕 <b>سانس جدید برای «{E(m.Title)}» اومد</b>\n" +
                  (first is null ? "" : $"\nنزدیک‌ترین سانس: {PersianFormat.Date(first.StartsAt)} ⏰ {PersianFormat.Time(first.StartsAt)}\n🏛 {E(first.Cinema)} · {E(first.Hall)}");
        // روزی حداکثر یک اعلان برای هر فیلم؛ اگر ادمین چند سانس پشت‌سرهم بگذارد پیام تکراری نمی‌رود
        await notify.BroadcastAsync(targets, "new_showtimes", txt, markup, dedupePrefix: $"newshow:{movieId}:{DateTime.Now:yyyyMMdd}");
    }

    /// <summary>زمان سانس عوض شد: برای دارندگان بلیتِ تأییدشده (ضروری، مستقل از تنظیمات).</summary>
    public async Task OnShowtimeTimeChangedAsync(int showtimeId, DateTime oldStart)
    {
        var ids = await db.Bookings.Where(b => b.ShowtimeId == showtimeId && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.AwaitingReview))
            .Select(b => b.Id).ToListAsync();
        foreach (var id in ids)
        {
            var b = await db.Bookings.Include(x => x.User).Include(x => x.Showtime).ThenInclude(s => s!.Movie)
                .Include(x => x.Showtime).ThenInclude(s => s!.Hall).ThenInclude(h => h!.Cinema).AsSplitQuery().FirstAsync(x => x.Id == id);
            var rows = new List<object[]>();
            if (await notify.WebButtonAsync("🎟 نمایش بلیت", $"/my-bookings?ticket={b.Id}") is { } web) rows.Add([web]);
            await notify.QueueAsync(b.User!, "booking_change", Messages.ShowtimeChanged(b, oldStart), rows.Count > 0 ? Kb.Markup(rows.ToArray()) : null,
                dedupeKey: $"chg:{b.Id}:{b.Showtime!.StartsAt:yyyyMMddHHmm}");
        }
        await db.SaveChangesAsync();
    }
}
